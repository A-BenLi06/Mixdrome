"""Adapt recovered MixRadio music templates to the client's Navidrome item models."""
import argparse
import copy
import json
import re
from pathlib import Path
from port_styles import ROOT, REFERENCE, objects, resource_key, render


def template(filename, key, output_key, bindings=None, reference=REFERENCE):
    metadata = json.loads((reference / filename).read_text(encoding="utf-8-sig"))
    obj = copy.deepcopy(next(o for o in objects(metadata["objects"][0]) if resource_key(o) == key))
    for prop in obj["properties"]:
        if prop["name"] in {"Key", "Name"}:
            prop["name"] = "Key"
            prop["values"][0]["value"] = output_key
    xml = render(obj, metadata, resource=True)
    for before, after in (bindings or {}).items():
        xml = xml.replace("Path=" + before + "}", "Path=" + after + "}")
    xml = re.sub(r"<MyMusicImageFader ([^>]*)/>",
                 lambda m: '<common:MixRadioArtwork ' + re.sub(r' Media="[^"]*"', '', m[1]) + ' Source="{Binding ImageUrl}" />', xml)
    xml = re.sub(r"<ImageFader ([^>]*)/>",
                 lambda m: '<common:MixRadioArtwork ' + m[1] + ' />', xml)
    xml = xml.replace('/Assets/PackArt-Explorer.Targetsize-256.png', 'ms-appx:///Assets/MixRadio/PackArt-Explorer.Targetsize-256.png')
    xml = xml.replace('/Assets/W8_Music_asset_DefaulPackArt_200px.png', 'ms-appx:///Assets/MixRadio/W8_Music_asset_DefaulPackArt_200px.png')
    xml = xml.replace('/Assets/Icons/play.png', 'ms-appx:///Assets/MixRadio/play.png')
    xml = re.sub(r'Text="\{Binding Path=SongCount, Converter=\{StaticResource SongCountConverter\}\}"', 'Text="{Binding CountText}"', xml)
    return '    <!-- ' + filename + ': ' + key + '; data bindings adapted to Navidrome. -->\n' + xml


def wordmark(reference):
    return '''    <!-- Mixdrome wordmark: the product name resolves through PRI resources. -->
    <ControlTemplate x:Key="MixdromeWordmarkTemplate">
        <Grid Height="41" HorizontalAlignment="Left" VerticalAlignment="Top">
            <TextBlock FontFamily="Segoe UI" FontSize="68" Margin="-3,-33,0,-16" Foreground="White" FlowDirection="LeftToRight">
                <Run x:Uid="BrandPrefix" FontWeight="Bold" /><Run x:Uid="BrandSuffix" FontWeight="Light" />
            </TextBlock>
        </Grid>
    </ControlTemplate>
'''


def offline_template(reference):
    xml = template('Common/NokiaMusicClient.json', 'RadioStationItemOfflineTemplate', 'MixRadioOfflineTemplate', reference=reference)
    xml = re.sub(r'Visibility="\{Binding Path=OfflineStatus, Converter=\{StaticResource EnumToVisibilityConverter\}, ConverterParameter=Pending\|Downloading\|Paused\|Failed\|Removing\|Resuming\}"',
                 'Visibility="{Binding CanPlay, Converter={StaticResource NavidromeHidden}}"', xml)
    xml = re.sub(r'Visibility="\{Binding Path=OfflineStatus, Converter=\{StaticResource EnumToVisibilityConverter\}, ConverterParameter=Downloading\|Paused\}"',
                 'Visibility="{Binding IsTransferring, Converter={StaticResource NavidromeVisibility}}"', xml)
    xml = xml.replace('Maximum="1"', 'Maximum="100"').replace('Path=OfflinePercentageProgress}', 'Path=Progress}')
    xml = xml.replace('x:Uid="HubOfflineStatusDownloading" Text="_Downloading..."', 'Text="{Binding StateText}"')
    xml = xml.replace('Text="{Binding Path=OfflinePercentageProgress, Converter={StaticResource PercentageConverter}}"', 'Text="{Binding ProgressPercentText}"')
    extra = '''                <StackPanel Grid.RowSpan="2" Margin="8,14,8,8" Visibility="{Binding CanPlay, Converter={StaticResource NavidromeHidden}}">
                    <TextBlock Text="{Binding StateText}" Style="{StaticResource SmallRegularText}" Visibility="{Binding IsTransferring, Converter={StaticResource NavidromeHidden}}" />
                    <TextBlock Text="{Binding Error}" Style="{StaticResource SmallRegularText}" Margin="0,8,0,0" MaxHeight="108" TextTrimming="WordEllipsis" ToolTipService.ToolTip="{Binding Error}" />
                </StackPanel>
'''
    # Insert only at the outer grid; the recovered download progress layout is retained.
    marker = '            </Grid.Children>\n        </Grid>\n    </DataTemplate>'
    if marker not in xml:
        raise ValueError('The recovered offline template structure changed')
    return xml.replace(marker, extra + marker)


def library_artist_template(reference):
    # Reused for artist results and inline album/artist/genre index headers.
    # Keep the GridViewItem as the only input target (no nested index button).
    xml = template('Views/MyMusic/MyMusicView.json', 'MyMusicIndexZoomedInTemplate',
                   'MixRadioLibraryArtistTemplate', {'Album': 'Title'}, reference)
    xml = xml.replace('<Button Style="{StaticResource IndexCardButtonStyle}">',
                      '<Border Background="Black" BorderBrush="{StaticResource NokiaPinkBrush}" BorderThickness="3" Padding="12,4,12,5" ToolTipService.ToolTip="{Binding Subtitle}">')
    xml = xml.replace('<Button.Content>', '<Border.Child>').replace('</Button.Content>', '</Border.Child>').replace('</Button>', '</Border>')
    return xml.replace('Style="{Binding Path=Album, Converter={StaticResource StringToFontStyleConverter}}"',
                       'Style="{StaticResource ExtraLargeLightSubHeaderText}" MaxLines="3" TextTrimming="CharacterEllipsis"')


def library_album_template(reference, genre=False):
    xml = template('Views/MyMusic/MyMusicView.json', 'MyMusicGenreZoomedInTemplate' if genre else 'MyMusicAlbumZoomedInTemplate',
                   'MixRadioLibraryGenreAlbumTemplate' if genre else 'MixRadioLibraryAlbumTemplate', {'Album': 'Title'}, reference)
    # Bound the text by whole lines: a three-line title, one artist line and
    # the count fit inside the recovered 130 px information panel.
    xml = xml.replace('MaxHeight="70" LineHeight="21"',
                      'MaxLines="3" LineHeight="21" ToolTipService.ToolTip="{Binding Title}"')
    xml = xml.replace('MaxHeight="45" Foreground="LightGray"',
                      'MaxLines="1" TextWrapping="NoWrap" ToolTipService.ToolTip="{Binding Artist}" Foreground="LightGray"')
    xml = xml.replace('Text="{Binding Path=ReleaseYear, Converter={StaticResource ReleaseYearConverter}}"', 'Text="{Binding YearText}"')
    return xml


def index_templates(reference):
    parts = []
    for original, output in [('ActiveCharacterGridTemplate', 'MixRadioActiveIndexTemplate'),
                             ('InactiveCharacterGridTemplate', 'MixRadioInactiveIndexTemplate'),
                             ('MyMusicArtistZoomedOutTemplate', 'MixRadioArtistIndexTemplate'),
                             ('MyMusicGenreZoomedOutTemplate', 'MixRadioGenreIndexTemplate')]:
        xml = template('Views/MyMusic/MyMusicView.json', original, output, {'Key': 'Title'}, reference)
        # GridViewItem owns pointer and keyboard activation; no nested Button.
        xml = re.sub(r'<Button(?:\s[^>]*)?>', '<Grid>', xml)
        xml = re.sub(r'<Button.RenderTransform>.*?</Button.RenderTransform>', '', xml, flags=re.S)
        xml = xml.replace('<Button.Content>', '').replace('</Button.Content>', '').replace('</Button>', '</Grid>')
        if 'Artist' in original or 'Genre' in original:
            replacement = ('<Grid Background="{StaticResource NokiaPinkBrush}">' +
                           ('<Image Source="ms-appx:///Assets/MixRadio/DefaultSemanticZoomArtist_158px.png" Width="158" Height="158" Stretch="Uniform"/>' if 'Artist' in original else '') + '</Grid>')
            xml = re.sub(r'<common:MixRadioArtwork[^>]*/>', lambda match: replacement, xml)
            xml = xml.replace('TextWrapping="Wrap"', 'TextWrapping="Wrap" TextTrimming="CharacterEllipsis" ToolTipService.ToolTip="{Binding Title}"')
        parts.append('\n'.join(line.rstrip() for line in xml.splitlines()) + '\n')
    parts.append('''    <DataTemplate x:Key="MixRadioSongIndexHeaderTemplate">
        <Grid Width="270" Height="65" Margin="0,0,25,0">
            <Border Background="Black" BorderBrush="{StaticResource NokiaPinkBrush}" BorderThickness="3" Padding="12,4,12,5">
                <TextBlock Text="{Binding Title}" Style="{StaticResource ExtraLargeLightSubHeaderText}" VerticalAlignment="Bottom"/>
            </Border>
        </Grid>
    </DataTemplate>
    <nav:MixRadioIndexTemplateSelector x:Key="MixRadioIndexSelector"
        ActiveTemplate="{StaticResource MixRadioActiveIndexTemplate}" InactiveTemplate="{StaticResource MixRadioInactiveIndexTemplate}"
        ArtistTemplate="{StaticResource MixRadioArtistIndexTemplate}" GenreTemplate="{StaticResource MixRadioGenreIndexTemplate}"/>
    <Style x:Key="MixRadioIndexItemStyle" TargetType="GridViewItem">
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Margin" Value="0"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        <Setter Property="VerticalContentAlignment" Value="Stretch"/>
        <Setter Property="IsEnabled" Value="{Binding IsEnabled}"/>
        <Setter Property="AutomationProperties.Name" Value="{Binding Title}"/>
    </Style>
''')
    return '\n'.join(parts)


def export(reference):
    parts = [
        template('Views/MyMusic/MyMusicPanel.json', 'MyMusicItemSmallTemplate', 'MixRadioHomeAlbumTemplate', reference=reference),
        template('Views/Search/SearchResultsView.json', 'ArtistTileTemplate', 'MixRadioHomeArtistTemplate', {'Artwork200x200': 'ImageUrl', 'Name': 'Title'}, reference),
        template('Common/NokiaMusicClient.json', 'CuratedRadioStationItemSmallTemplate', 'MixRadioHomePlaylistTemplate', reference=reference),
        template('Common/NokiaMusicClient.json', 'RadioStationItemExtraLargeTemplate', 'MixRadioHomeHeroTemplate', reference=reference),
        template('Common/NokiaMusicClient.json', 'MyMusicInfoItemTemplate', 'MyMusicInfoItemTemplate', {'Album': 'Title'}, reference),
        template('Common/NokiaMusicClient.json', 'RadioStationInfoItemTemplate', 'RadioStationInfoItemTemplate', {'Name': 'Title'}, reference),
        template('Common/DesignAssets.json', 'MyMusicAlbumPlayIcon', 'MyMusicAlbumPlayIcon', reference=reference),
        library_album_template(reference),
        library_artist_template(reference),
        template('Views/MyMusic/MyMusicView.json', 'MyMusicSongZoomedInTemplate', 'NavidromeTrackTemplate', {'Name': 'Title'}, reference),
        template('Views/MyMusic/MyMusicPanel.json', 'MyMusicItemSmallTemplate', 'NavidromeTileTemplate', reference=reference),
    ]
    preamble = '''<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:cal="using:Caliburn.Micro"
    xmlns:common="using:Mixdrome.Common" xmlns:nav="using:Mixdrome.Navidrome"
    xmlns:converters="using:Mixdrome.Converters">
    <cal:BooleanToVisibilityConverter x:Key="NavidromeVisibility" />
    <cal:BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter" />
    <converters:InvertedBooleanToVisibilityConverter x:Key="NavidromeHidden" />
    <ControlTemplate x:Key="MyMusicArtworkTemplate">
        <common:MixRadioArtwork Source="{Binding ImageUrl}" DefaultSource="ms-appx:///Assets/MixRadio/PackArt-Explorer.Targetsize-256.png" />
    </ControlTemplate>
    <ControlTemplate x:Key="CuratedRadioStationArtworkTemplate">
        <common:MixRadioArtwork Source="{Binding ImageUrl}" DefaultSource="ms-appx:///Assets/MixRadio/W8_Music_asset_DefaulPackArt_200px.png" />
    </ControlTemplate>
'''
    extras = '''
    <!-- StandardButtonStyle's 162 px layout rendered inside the clickable GridViewItem. -->
    <DataTemplate x:Key="MixRadioNavigationTileTemplate">
        <Grid Width="162" Height="162" Background="{StaticResource NokiaPinkBrush}">
            <Grid.RowDefinitions><RowDefinition Height="90" /><RowDefinition Height="*" /></Grid.RowDefinitions>
            <Grid HorizontalAlignment="Center" VerticalAlignment="Bottom" FlowDirection="LeftToRight">
                <TextBlock Text="&#xE17F;" FontFamily="Segoe UI Symbol" FontSize="20" Foreground="Transparent" />
                <TextBlock Text="&#xE17E;" FontFamily="Segoe UI Symbol" FontSize="20" />
                <TextBlock Text="{Binding Glyph}" FontFamily="Segoe UI Symbol" FontSize="20" HorizontalAlignment="Center" VerticalAlignment="Center" />
            </Grid>
            <TextBlock Grid.Row="1" Text="{Binding Label}" Margin="15,15,15,0" TextAlignment="Center" Style="{StaticResource Medium2LightText}" />
        </Grid>
    </DataTemplate>
    <!-- MyMusic song-row layout for entities without artwork, such as a share or radio station. -->
    <DataTemplate x:Key="MixRadioLibraryTextTemplate">
        <Grid Width="270" Height="65" Margin="0,0,25,0">
            <Rectangle Fill="Gray" Opacity="0.3" />
            <StackPanel Margin="10,5,10,8">
                <TextBlock Text="{Binding Title}" Style="{StaticResource NormalLightText}" TextWrapping="NoWrap" />
                <TextBlock Text="{Binding Subtitle}" Style="{StaticResource SmallGreyText}" TextWrapping="NoWrap" />
                <TextBlock Text="{Binding Kind}" Style="{StaticResource SmallGreyText}" TextWrapping="NoWrap" />
            </StackPanel>
        </Grid>
    </DataTemplate>
    <nav:MixRadioHomeTemplateSelector x:Key="MixRadioHomeSmallSelector"
        AlbumTemplate="{StaticResource MixRadioHomeAlbumTemplate}" ArtistTemplate="{StaticResource MixRadioHomeArtistTemplate}"
        PlaylistTemplate="{StaticResource MixRadioHomePlaylistTemplate}" LinkTemplate="{StaticResource MixRadioNavigationTileTemplate}" />
    <nav:MixRadioHomeTemplateSelector x:Key="MixRadioHomeSelector"
        AlbumTemplate="{StaticResource MixRadioHomeAlbumTemplate}" ArtistTemplate="{StaticResource MixRadioHomeArtistTemplate}"
        PlaylistTemplate="{StaticResource MixRadioHomePlaylistTemplate}" HeroTemplate="{StaticResource MixRadioHomeHeroTemplate}"
        LinkTemplate="{StaticResource MixRadioNavigationTileTemplate}" />
    <nav:MixRadioLibraryTemplateSelector x:Key="MixRadioLibrarySelector"
        IndexTemplate="{StaticResource MixRadioLibraryArtistTemplate}" SongIndexTemplate="{StaticResource MixRadioSongIndexHeaderTemplate}"
        GenreAlbumTemplate="{StaticResource MixRadioLibraryGenreAlbumTemplate}"
        AlbumTemplate="{StaticResource MixRadioLibraryAlbumTemplate}" ArtistTemplate="{StaticResource MixRadioLibraryArtistTemplate}"
        SongTemplate="{StaticResource NavidromeTrackTemplate}" TextTemplate="{StaticResource MixRadioLibraryTextTemplate}" />
    <Style x:Key="MixRadioLibraryAlbumItemStyle" TargetType="GridViewItem" BasedOn="{StaticResource MyMusicItemContainerZoomedInAlbumStyle}">
        <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
        <Setter Property="AutomationProperties.HelpText" Value="{Binding Subtitle}" />
    </Style>
    <Style x:Key="MixRadioLibrarySongItemStyle" TargetType="GridViewItem" BasedOn="{StaticResource MyMusicItemContainerZoomedInSongStyle}">
        <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
        <Setter Property="AutomationProperties.HelpText" Value="{Binding Subtitle}" />
    </Style>
    <nav:MixRadioLibraryContainerStyleSelector x:Key="MixRadioLibraryContainerSelector"
        AlbumStyle="{StaticResource MixRadioLibraryAlbumItemStyle}" SongStyle="{StaticResource MixRadioLibrarySongItemStyle}" />
    <Style x:Key="MixRadioLibraryMenuStyle" TargetType="FlyoutPresenter">
        <Setter Property="Background" Value="#211D1E" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="MinWidth" Value="0" />
    </Style>
</ResourceDictionary>
'''
    # Dependencies precede the recovered item templates in the resource dictionary.
    order = [4, 5, 6, 0, 1, 2, 3, 7, 8, 9, 10]
    return preamble + wordmark(reference) + '\n'.join(parts[index] for index in order) + library_album_template(reference, genre=True) + offline_template(reference) + index_templates(reference) + extras


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=REFERENCE)
    parser.add_argument('--output', type=Path, default=ROOT / 'Client/Navidrome/NavidromeResources.xaml')
    args = parser.parse_args()
    args.output.write_text(export(args.reference), encoding='utf-8')
    print('Ported music templates to ' + str(args.output))
