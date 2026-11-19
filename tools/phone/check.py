"""Audit phone source, resources and package wiring without deployment or SDK requirements."""
import hashlib
import json
import re
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PHONE = ROOT / 'Client.Phone'
BACKGROUND = ROOT / 'Client.Phone.BackgroundAudioTask'
XAML = '{http://schemas.microsoft.com/winfx/2006/xaml}'
IGNORED = {'bin', 'obj', 'AppPackages'}
ITEM_TYPES = {'Compile', 'Page', 'ApplicationDefinition', 'Content', 'PRIResource',
              'AppxManifest', 'None', 'ProjectReference'}
TEMPLATE_SCOPES = {'DataTemplate', 'ControlTemplate', 'ItemsPanelTemplate', 'ResourceDictionary'}
# Verified against Windows Phone Kits/8.1/Include/abi/Xaml/Design/generic.xaml.
# Keep exact names: accepting arbitrary *Theme* or SystemColor* names would hide
# references to resources introduced in Windows 10.
PLATFORM_RESOURCES = frozenset({
    'ApplicationPageBackgroundThemeBrush', 'ApplicationForegroundThemeBrush',
    'ApplicationSecondaryForegroundThemeBrush', 'ContentControlThemeFontFamily',
    'ContentControlFontSize', 'PhoneAccentBrush', 'PhoneForegroundBrush',
    'PhoneFontFamilyNormal', 'PhoneFontFamilyLight', 'PhoneFontFamilySemiLight',
    'PhoneFontFamilySemiBold', 'SystemColorControlAccentColor',
    'TextBoxForegroundThemeBrush', 'TextBoxBackgroundThemeBrush',
})
UID_TARGETS = {
    'Text': {'TextBlock', 'TextBox', 'Run'},
    'Label': {'AppBarButton', 'AppBarToggleButton'},
    'PlaceholderText': {'TextBox', 'PasswordBox', 'SearchBox'},
    'Content': {'Button', 'ToggleButton', 'CheckBox', 'RadioButton', 'ContentControl'},
    'Header': {'Pivot', 'PivotItem', 'Hub', 'HubSection', 'ComboBox', 'TextBox',
               'PasswordBox', 'ToggleSwitch', 'ListView', 'GridView'},
    'OnContent': {'ToggleSwitch'}, 'OffContent': {'ToggleSwitch'},
}
FORBIDDEN_CONTROLS = {'NavigationView', 'AdaptiveTrigger', 'MediaPlayerElement',
                      'RelativePanel', 'SplitView'}
issues = []
counts = {'files': 0, 'xaml': 0, 'strings': 0, 'resources': 0, 'assets': 0}


def label(path):
    return path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)


def local(tag):
    return tag.rsplit('}', 1)[-1]


def read(path):
    return path.read_text(encoding='utf-8-sig')


def parse(path):
    try:
        return ET.parse(path).getroot()
    except (OSError, ET.ParseError) as error:
        issues.append(f'{label(path)}: {error}')
        return ET.Element('Invalid')


def project_files(folder, filename):
    tree = parse(folder / filename)
    included, logical = set(), {}
    parents = {child: parent for parent in tree.iter() for child in parent}
    for node in tree.iter():
        if local(node.tag) == 'ItemGroup':
            parent = parents.get(node)
            valid_parent = parent is tree or (parent is not None and local(parent.tag) == 'Target' and parents.get(parent) is tree)
            if not valid_parent:
                issues.append(f'{label(folder / filename)}: ItemGroup cannot be nested inside item metadata')
    for node in tree.iter():
        name = node.get('Include')
        if local(node.tag) not in ITEM_TYPES or not name:
            continue
        link = next((child.text for child in node if local(child.tag) == 'Link'), None)
        normalized = name.replace('\\', '/')
        qualified_images = local(node.tag) == 'Content' and normalized == 'Assets/*.scale-*.png'
        if qualified_images:
            # The branding project deliberately packages this one scale-qualified
            # image family glob. Enumerate real files; arbitrary item globs, Link,
            # Exclude and MSBuild expressions still require explicit resolution.
            if link or node.get('Exclude') or node.get('Condition'):
                issues.append(f'{label(folder / filename)}: scale-qualified content cannot use Link, Exclude or Condition: {name}')
                continue
            paths = sorted((folder / 'Assets').glob('*.scale-*.png'))
            if not paths:
                issues.append(f'{label(folder / filename)}: scale-qualified content glob has no files: {name}')
            for image in paths:
                if not re.fullmatch(r'.+\.scale-(100|140|240)\.png', image.name):
                    issues.append(f'{label(image)}: invalid Windows Phone 8.1 scale-qualified image name')
        elif '$(' in name or any(char in name for char in '*?['):
            issues.append(f'{label(folder / filename)}: phone items must resolve explicitly: {name}')
            continue
        else:
            paths = [folder / normalized]
        for source_path in paths:
            path = source_path.resolve()
            if not path.is_file():
                issues.append(f'{label(folder / filename)}: missing included file {name}')
            if path in included:
                issues.append(f'{label(folder / filename)}: duplicate included file {label(path)}')
            included.add(path)
            if local(node.tag) == 'ProjectReference':
                metadata = [child for child in node if local(child.tag) == 'Project']
                if len(metadata) != 1 or len(metadata[0]) != 0:
                    issues.append(f'{label(folder / filename)}: ProjectReference needs a plain Project GUID: {name}')
                else:
                    try:
                        declared_guid = uuid.UUID((metadata[0].text or '').strip().strip('{}'))
                        if path.is_file():
                            referenced = parse(path)
                            target_guid = next((value.text for value in referenced.iter() if local(value.tag) == 'ProjectGuid'), '')
                            if declared_guid != uuid.UUID((target_guid or '').strip().strip('{}')):
                                issues.append(f'{label(folder / filename)}: ProjectReference GUID differs from {name}')
                    except ValueError:
                        issues.append(f'{label(folder / filename)}: invalid ProjectReference GUID for {name}')
            if local(node.tag) in {'Content', 'PRIResource', 'Page', 'ApplicationDefinition'}:
                target = path.relative_to(folder).as_posix() if qualified_images else (link or name).replace('\\', '/').lstrip('/')
                if target in logical and logical[target] != path:
                    issues.append(f'{label(folder / filename)}: duplicate package path {target}')
                logical[target] = path
    for path in folder.rglob('*'):
        if path.is_file() and path.suffix in {'.cs', '.xaml', '.resw', '.appxmanifest'} and not set(path.relative_to(folder).parts) & IGNORED:
            counts['files'] += 1
            if path.resolve() not in included:
                issues.append(f'{label(path)}: missing explicit project entry')
    for property_name, expected in {'TargetPlatformIdentifier': 'WindowsPhoneApp',
                                    'TargetPlatformVersion': '8.1'}.items():
        value = next((node.text for node in tree.iter() if local(node.tag) == property_name), None)
        if value != expected:
            issues.append(f'{label(folder / filename)}: {property_name} must be {expected}')
    return tree, included, logical


def resource_keys(path, visited=None):
    visited = set() if visited is None else visited
    path = path.resolve()
    if path in visited:
        return set()
    visited.add(path)
    document = parse(path)
    keys = {node.get(XAML + 'Key') for node in document.iter() if node.get(XAML + 'Key')}
    for container in document.iter():
        if local(container.tag) == 'ResourceDictionary' or local(container.tag).endswith('.Resources'):
            direct_keys = [node.get(XAML + 'Key') or node.get(XAML + 'Name') for node in container]
            explicit = [key for key in direct_keys if key]
            if len(explicit) != len(set(explicit)):
                issues.append(f'{label(path)}: duplicate resource key in one dictionary')
            keys.update(explicit)
    for node in document.iter():
        if local(node.tag) != 'ResourceDictionary' or not node.get('Source'):
            continue
        source = node.get('Source').replace('\\', '/')
        if source.startswith('ms-appx:///'):
            relative = source[len('ms-appx:///'):]
            merged = phone_logical.get(relative, PHONE / relative)
        elif '://' in source:
            issues.append(f'{label(path)}: resource dictionary must be a packaged file: {source}')
            continue
        else:
            merged = PHONE / source.lstrip('/') if source.startswith('/') else path.parent / source
        if merged.resolve() not in phone_included:
            issues.append(f'{label(path)}: merged dictionary not included: {label(merged.resolve())}')
        if not merged.is_file():
            issues.append(f'{label(path)}: missing merged dictionary {source}')
        else:
            keys.update(resource_keys(merged, visited))
    return keys


def audit_names(node, path, scope=None):
    if scope is None or local(node.tag) in TEMPLATE_SCOPES:
        scope = set()
    name = node.get(XAML + 'Name')
    if name:
        if name in scope:
            issues.append(f'{label(path)}: duplicate XAML name {name} within a namescope')
        scope.add(name)
    for child in node:
        audit_names(child, path, scope)


def first_argument(source, start):
    """Extract a helper's first C# argument, including conditional literals."""
    depth, index, quoted = 0, start, False
    while index < len(source):
        char = source[index]
        if quoted:
            if char == '\\':
                index += 2
                continue
            if char == '"':
                quoted = False
        elif char == '"':
            quoted = True
        elif char == '(':
            depth += 1
        elif char == ')':
            if depth == 0:
                return source[start:index]
            depth -= 1
        elif char == ',' and depth == 0:
            return source[start:index]
        index += 1
    return ''


def check_string(key, path):
    counts['strings'] += 1
    if string_prefix + key not in strings:
        issues.append(f'{label(path)}: missing phone string {string_prefix + key}')


def packaged_asset(relative):
    relative = relative.replace('\\', '/').lstrip('/')
    if relative in phone_logical:
        return True
    # Manifest/XAML use an unqualified PNG name; MakePRI resolves only variants
    # from the same packaged directory and basename with WP8.1 scale qualifiers.
    if not relative.endswith('.png') or '.scale-' in relative:
        return False
    pattern = re.compile(re.escape(relative[:-4]) + r'\.scale-(100|140|240)\.png')
    return any(pattern.fullmatch(name) for name in phone_logical)


def check_packaged_assets(source, path):
    for relative in re.findall(r'ms-appx:///([^"\s<>]+)', source):
        relative = relative.replace('\\', '/')
        if not packaged_asset(relative):
            issues.append(f'{label(path)}: unpackaged app asset {relative}')
    # Check literal image URIs, including assets linked from the desktop project.
    for uri in re.findall(r'\bSource="([^"{}]+\.(?:png|jpg|jpeg))"', source):
        if '://' in uri:
            continue
        relative = uri.replace('\\', '/').lstrip('/')
        local_path = (path.parent / relative).resolve()
        if not packaged_asset(relative) and local_path not in phone_included:
            issues.append(f'{label(path)}: unpackaged app asset {uri}')


phone_project, phone_included, phone_logical = project_files(PHONE, 'Mixdrome.Phone.csproj')
background_project, background_included, _ = project_files(BACKGROUND, 'Mixdrome.Phone.BackgroundAudioTask.csproj')
default_language = next((node.text for node in phone_project.iter() if local(node.tag) == 'DefaultLanguage'), None)
resource_path = PHONE / 'Strings' / (default_language or '') / 'Resources.resw'
resource_document = parse(resource_path)
strings = set()
for translated_path in sorted(path for path in phone_included if path.suffix == '.resw' and path.is_file()):
    folded_names = {}
    for data in parse(translated_path).findall('data'):
        name = data.get('name', '')
        folded = name.casefold()
        if folded in folded_names:
            issues.append(f'{label(translated_path)}: MakePRI resource name collision {folded_names[folded]} / {name}')
        folded_names[folded] = name
for data in resource_document.findall('data'):
    name = data.get('name')
    if name in strings:
        issues.append(f'{label(resource_path)}: duplicate string {name}')
    strings.add(name)
    if data.find('value') is None or not data.findtext('value'):
        issues.append(f'{label(resource_path)}: empty string {name}')
if resource_path.resolve() not in phone_included:
    issues.append(f'{label(resource_path)}: default resource file is not packaged')
helper_path = PHONE / 'Common/PhoneText.cs'
helper = read(helper_path) if helper_path.is_file() else ''
prefix_match = re.search(r'GetString\(\s*"([^"]+)"\s*\+\s*key\s*\)', helper)
string_prefix = prefix_match.group(1) if prefix_match else ''
if not prefix_match:
    issues.append(f'{label(helper_path)}: cannot determine the phone resource prefix')
application_keys = resource_keys(PHONE / 'App.xaml')

for path in sorted(phone_included | background_included):
    if not path.is_file() or path.suffix not in {'.cs', '.xaml'}:
        continue
    source = read(path)
    check_packaged_assets(source, path)
    if path.suffix == '.xaml':
        counts['xaml'] += 1
        document = parse(path)
        audit_names(document, path)
        visible_keys = application_keys | resource_keys(path) | PLATFORM_RESOURCES
        for kind, key in re.findall(r'\{(StaticResource|ThemeResource)\s+([^}\s]+)\s*\}', source):
            counts['resources'] += 1
            if key not in visible_keys:
                issues.append(f'{label(path)}: unknown WP8.1 {kind} {key}')
        for node in document.iter():
            tag = local(node.tag)
            if tag in FORBIDDEN_CONTROLS:
                issues.append(f'{label(path)}: {tag} is unavailable on WP8.1 WinRT')
            uid = node.get(XAML + 'Uid')
            if not uid:
                continue
            properties = [name[len(uid) + 1:] for name in strings if name and name.startswith(uid + '.')]
            if not properties:
                issues.append(f'{label(path)}: missing x:Uid resource properties for {uid}')
            for property_name in properties:
                allowed = UID_TARGETS.get(property_name)
                if allowed is not None and tag not in allowed:
                    issues.append(f'{label(path)}: {uid}.{property_name} cannot target {tag}')
        if re.search(r'\{x:Bind\b|\bxmlns[^=]*="using:Microsoft\.UI\.', source):
            issues.append(f'{label(path)}: Windows 10/WinUI XAML binding or namespace')
        if re.search(r'Segoe WP (?:Light|SemiLight|Semibold|SemiBold|Bold|Black)\b', source):
            issues.append(f'{label(path)}: DirectWrite Segoe WP weights must use FontFamily="Segoe WP" with FontWeight')
    else:
        if re.search(r'\busing\s+(?:Microsoft\.UI|Windows\.UI\.Composition)\b|\bApiInformation\s*\.|\bMediaPlaybackList\b', source):
            issues.append(f'{label(path)}: Windows 10/WinUI API is unavailable on WP8.1')
        for match in re.finditer(r'PhoneText\.(?:Get|Format)\s*\(', source):
            expression = first_argument(source, match.end())
            if re.fullmatch(r'\s*"[^"]+"\s*\+\s*\w+\s*', expression):
                # Authentication labels enumerate the production protocol enum.
                # Read its members so adding an authentication mode requires a
                # corresponding resource without baking mode names into this tool.
                prefix = re.search(r'"([^"]+)"', expression).group(1)
                variable = re.search(r'\+\s*(\w+)\s*$', expression).group(1)
                enum_match = re.search(r'Cast<(\w+)>\(\)\.Select\(\s*' + re.escape(variable) + r'\s*=>', source)
                if not enum_match:
                    issues.append(f'{label(path)}: unresolved dynamic PhoneText lookup {expression.strip()}')
                    continue
                enum_name = enum_match.group(1)
                declarations = [re.search(r'\benum\s+' + re.escape(enum_name) + r'\s*\{([^}]+)\}', read(candidate))
                                for candidate in (ROOT / 'Client.Common').rglob('*.cs')
                                if not set(candidate.relative_to(ROOT / 'Client.Common').parts) & IGNORED]
                declaration = next((value for value in declarations if value), None)
                if not declaration:
                    issues.append(f'{label(path)}: cannot locate protocol enum {enum_name}')
                    continue
                for member in declaration.group(1).split(','):
                    name = re.match(r'\s*(\w+)', member)
                    if name:
                        check_string(prefix + name.group(1), path)
            else:
                for key in re.findall(r'"([^"\\]+)"', expression):
                    check_string(key, path)

manifest_path = PHONE / 'Package.appxmanifest'
manifest = parse(manifest_path)
app_class = parse(PHONE / 'App.xaml').get(XAML + 'Class')
applications = [node for node in manifest.iter() if local(node.tag) == 'Application']
if len(applications) != 1 or applications[0].get('EntryPoint') != app_class:
    issues.append(f'{label(manifest_path)}: application entrypoint does not match App.xaml')
app_namespace, _, app_name = (app_class or '').rpartition('.')
app_code = read(PHONE / 'App.xaml.cs')
if not re.search(r'\bnamespace\s+' + re.escape(app_namespace) + r'\b', app_code) or not re.search(r'\bclass\s+' + re.escape(app_name) + r'\b', app_code):
    issues.append(f'{label(manifest_path)}: application entrypoint has no matching App.xaml.cs class')
extensions = [node for node in manifest.iter() if local(node.tag) == 'Extension' and node.get('Category') == 'windows.backgroundTasks']
audio_extensions = [node for node in extensions if any(local(child.tag) == 'Task' and child.get('Type') == 'audio' for child in node.iter())]
if len(audio_extensions) != 1:
    issues.append(f'{label(manifest_path)}: exactly one background audio task must be registered')
else:
    entrypoint = audio_extensions[0].get('EntryPoint', '')
    namespace, _, class_name = entrypoint.rpartition('.')
    task_sources = [read(path) for path in background_included if path.is_file() and path.suffix == '.cs']
    if not any(re.search(r'\bnamespace\s+' + re.escape(namespace) + r'\b', source) and
               re.search(r'\bclass\s+' + re.escape(class_name) + r'\s*:\s*IBackgroundTask\b', source) for source in task_sources):
        issues.append(f'{label(manifest_path)}: audio entrypoint has no matching IBackgroundTask class')
    if (BACKGROUND / 'Mixdrome.Phone.BackgroundAudioTask.csproj').resolve() not in phone_included:
        issues.append(f'{label(manifest_path)}: audio project is not referenced by phone app')
for node in manifest.iter():
    if local(node.tag) == 'Logo' and not packaged_asset(node.text or ''):
        issues.append(f'{label(manifest_path)}: unpackaged manifest logo {node.text}')
    for name, value in node.attrib.items():
        if name.endswith('Logo') or name == 'Image':
            if not packaged_asset(value):
                issues.append(f'{label(manifest_path)}: unpackaged manifest image {value}')

asset_manifest = json.loads(read(ROOT / 'tools/mixradio/phone-assets.json'))
for asset in asset_manifest['files']:
    path = ROOT / asset['destination']
    counts['assets'] += 1
    if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != asset['sha256']:
        issues.append(f'{label(path)}: original phone asset SHA-256 mismatch')
    if path.resolve() not in phone_included:
        issues.append(f'{label(path)}: original phone asset missing from project')

if issues:
    raise SystemExit('\n'.join(dict.fromkeys(issues)))
print('PASS: phone source/resource/package audit:', ', '.join(f'{count} {name}' for name, count in counts.items()),
      '- native rendering, gestures and background playback require device/emulator evidence.')
