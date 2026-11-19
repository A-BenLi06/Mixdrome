"""Audit resource lookup and project inclusion without claiming native acceptance."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
client = root / 'Client'
resources = {data.get('name') for data in ET.parse(client / 'Strings/en-US/Resources.resw').findall('data')}
project = ET.parse(client / 'Mixdrome.csproj')
included = {node.get('Include', '').replace('\\', '/') for node in project.iter()}
issues = []
lookups = 0
static_lookups = 0
xaml_namespace = '{http://schemas.microsoft.com/winfx/2006/xaml}'


def resource_keys(path, visited=None):
    visited = set() if visited is None else visited
    path = path.resolve()
    if path in visited:
        return set()
    visited.add(path)
    tree = ET.parse(path)
    keys = {node.get(xaml_namespace + 'Key') for node in tree.iter() if node.get(xaml_namespace + 'Key')}
    # Windows 8.1 also uses x:Name as a dictionary key, notably in the existing light styles.
    for container in tree.iter():
        if container.tag.endswith('ResourceDictionary') or container.tag.endswith('.Resources'):
            keys.update(node.get(xaml_namespace + 'Name') for node in container if node.get(xaml_namespace + 'Name'))
    for node in tree.iter():
        source = node.get('Source') if node.tag.endswith('ResourceDictionary') else None
        if not source:
            continue
        if source.startswith('ms-appx:///'):
            merged = client / source[len('ms-appx:///'):]
        elif '://' in source:
            continue
        else:
            merged = client / source.lstrip('/') if source.startswith('/') else path.parent / source
        if not merged.is_file():
            issues.append('Missing merged resource dictionary: ' + str(merged))
        else:
            keys.update(resource_keys(merged, visited))
    return keys


def audit_names(node, path, scope=None):
    tag = node.tag.split('}')[-1]
    if scope is None or tag in ('DataTemplate', 'ControlTemplate', 'ItemsPanelTemplate', 'ResourceDictionary'):
        scope = set()
    name = node.get(xaml_namespace + 'Name')
    if name:
        if name in scope:
            issues.append('Duplicate XAML name: ' + name + ' in ' + path)
        scope.add(name)
    for child in node:
        audit_names(child, path, scope)


def audit_storyboards(node, path):
    # Templates own separate namescopes; each storyboard must name a part in
    # its own template. Also reject untyped colors assigned to Brush properties.
    def scoped_parts(part):
        yield part
        for child in part:
            if child.tag.split('}')[-1] not in {'ControlTemplate', 'DataTemplate', 'ItemsPanelTemplate', 'ResourceDictionary'}:
                yield from scoped_parts(child)

    for template in node.iter():
        if template.tag.split('}')[-1] != 'ControlTemplate':
            continue
        parts = list(scoped_parts(template))
        names = {part.get(xaml_namespace + 'Name') for part in parts}
        for animation in parts:
            target = animation.get('Storyboard.TargetName')
            if target and target not in names:
                issues.append('Missing storyboard target: ' + target + ' in ' + path)
            if animation.tag.split('}')[-1] != 'ObjectAnimationUsingKeyFrames':
                continue
            if animation.get('Storyboard.TargetProperty') not in {'Fill', 'Stroke', 'Background', 'Foreground', 'BorderBrush'}:
                continue
            for frame in animation.iter():
                if re.fullmatch(r'#[0-9A-Fa-f]{6,8}', frame.get('Value', '')):
                    issues.append('Brush animation has an untyped color value in ' + path)


application_keys = resource_keys(client / 'App.xaml')
paths = (list((client / 'Navidrome').iterdir()) + list((client / 'Main').iterdir()) +
         list((client / 'Shell').glob('ShellView*')) +
         list((client / 'Settings').glob('SettingsView*')) + list((client / 'Common').glob('MixRadio*')) +
         list((client / 'Common').glob('LibrarySearch*')))
for path in paths:
    if path.suffix not in ('.cs', '.xaml'):
        continue
    relative = path.relative_to(client).as_posix()
    if relative not in included:
        issues.append('Missing project entry: ' + relative)
    source = path.read_text(encoding='utf-8-sig')
    if path.suffix == '.xaml':
        document = ET.parse(path).getroot()
        audit_names(document, relative)
        audit_storyboards(document, relative)
        for node in document.iter():
            if node.tag.endswith('.ItemContainerTransitions') and len(node) and not node[0].tag.endswith('}TransitionCollection'):
                issues.append('Item transitions must create their own TransitionCollection: ' + relative)
            uid = node.get(xaml_namespace + 'Uid')
            if not uid:
                continue
            tag = node.tag.split('}')[-1]
            properties = [value[len(uid) + 1:] for value in resources if value.startswith(uid + '.')]
            if not properties:
                issues.append('Missing property resource: ' + uid + ' in ' + relative)
            # These properties are easy to confuse when adapting a template to a different native control.
            for name, allowed in {'Text': {'TextBlock', 'TextBox', 'Run'}, 'Label': {'AppBarButton', 'AppBarToggleButton'},
                                  'PlaceholderText': {'TextBox', 'PasswordBox', 'SearchBox'},
                                  'Content': {'Button', 'ToggleButton', 'CheckBox', 'RadioButton', 'ContentControl'}}.items():
                if name in properties and tag not in allowed:
                    issues.append('Resource ' + uid + '.' + name + ' cannot target ' + tag + ' in ' + relative)
        visible_keys = application_keys | resource_keys(path)
        for key in re.findall(r'\{StaticResource\s+([^}\s]+)\s*\}', source):
            # Native SDK theme resources are supplied by Windows. Audit every
            # project-owned key, including recovered vector/template dependencies.
            if key not in visible_keys and ('Theme' in key or re.fullmatch(r'SystemColor.+Color', key)):
                continue
            static_lookups += 1
            if key not in visible_keys:
                issues.append('Missing project resource: ' + key + ' in ' + relative)
    for key in re.findall(r'NavidromeText\.Get\("([^"]+)"\)', source):
        lookups += 1
        if 'Navidrome_Text_' + key not in resources:
            issues.append('Missing string: ' + key + ' in ' + relative)
    for title, empty in re.findall(r'Section\("[^"]+",\s*"([^"]+)",\s*"([^"]+)"', source):
        for key in (title, empty):
            lookups += 1
            if 'Navidrome_Text_' + key not in resources:
                issues.append('Missing home string: ' + key + ' in ' + relative)
    for key in re.findall(r'x:Uid="(Navidrome_[^"]+)"', source):
        lookups += 1
        if not any(value.startswith(key + '.') for value in resources):
            issues.append('Missing property resource: ' + key + ' in ' + relative)
for key in ('albums', 'artists', 'songs', 'genres', 'playlists', 'radio', 'activity', 'random', 'favourites', 'search', 'bookmarks', 'shares'):
    if 'Navidrome_Text_Section' + key not in resources:
        issues.append('Missing section title: ' + key)
project_pages = [client / node.get('Include').replace('\\', '/') for node in project.iter()
                 if node.tag.endswith('}Page') and node.get('Include')]
uses_inline_search = any(re.search(r'<SearchBox\b', path.read_text(encoding='utf-8-sig'))
                         for path in project_pages if path.is_file())
if uses_inline_search:
    # This desktop shell owns a single view. Activating its legacy SearchPane
    # prevents the native SearchBox constructor from creating any page that uses it.
    for node in project.iter():
        if not node.tag.endswith('}Compile') or not node.get('Include'):
            continue
        path = client / node.get('Include').replace('\\', '/')
        if path.is_file() and re.search(r'\bSearchPane\s*\.\s*GetForCurrentView\s*\(', path.read_text(encoding='utf-8-sig')):
            issues.append('Inline SearchBox conflicts with legacy SearchPane initialization: ' + str(path.relative_to(root)))
for view_relative in ('Main/MainView.xaml', 'Navidrome/NavidromePlayerView.xaml'):
    view_doc = ET.parse(client / view_relative).getroot()
    named = {node.get(xaml_namespace + 'Name'): node for node in view_doc.iter() if node.get(xaml_namespace + 'Name')}
    drawer = named.get('QueueDrawer')
    dismiss = named.get('QueueDismiss')
    if drawer is None or drawer.get('HorizontalAlignment') != 'Left':
        issues.append('QueueDrawer must align Left to match left pane transition: ' + view_relative)
    if dismiss is None or dismiss.get('Style') != '{StaticResource MixRadioDismissButtonStyle}':
        issues.append('QueueDismiss must use MixRadioDismissButtonStyle: ' + view_relative)
if issues:
    raise SystemExit('\n'.join(issues))
print('PASS:', lookups, 'desktop string lookups,', static_lookups,
      'project resource references, XAML names and all Navidrome project entries. This is a source audit, not a native UI pass.')
