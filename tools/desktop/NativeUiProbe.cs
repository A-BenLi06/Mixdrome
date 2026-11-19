namespace Mixdrome.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using Mixdrome.Main;
    using Mixdrome.Navidrome;
    using Mixdrome.Shell;
    using Windows.Data.Json;
    using Windows.Foundation;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Automation.Peers;
    using Windows.UI.Xaml.Automation.Provider;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;

    // Included only in an explicit NativeUiProbe build. Screenshots come from the
    // running app's actual XAML tree; normal builds contain no capture/navigation driver.
    internal static class NativeUiProbe
    {
        private static bool _fatal;
        internal static void RecordFatalAndExit(string message, Exception exception)
        {
            if (_fatal) return;
            _fatal = true;
            try
            {
                var text = DateTimeOffset.Now.ToString("O") + Environment.NewLine + message + Environment.NewLine + exception;
                Task.Run(async () =>
                {
                    var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("native-ui-fatal.txt", CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(file, text);
                }).GetAwaiter().GetResult();
            }
            finally { Application.Current.Exit(); }
        }

        internal static async Task RunAsync(ShellView shell, INavigationService navigation)
        {
#if NATIVE_PLAYBACK_PROBE
            await NativePlaybackProbe.RunAsync(shell);
#elif NATIVE_UI_OBSERVE
            await ObserveAsync(shell);
#else
            try
            {
                var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync("native-ui-probe", CreationCollisionOption.OpenIfExists);
                var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
                var folder = await root.CreateFolderAsync(stamp, CreationCollisionOption.FailIfExists);
                var report = new JsonObject();
                report["createdAt"] = JsonValue.CreateStringValue(DateTimeOffset.Now.ToString("O"));
                report["directory"] = JsonValue.CreateStringValue(folder.Path);
                var pages = new JsonArray();
                report["pages"] = pages;
                pages.Add(await Capture(shell, folder, "home-startup"));
                var errorView = Descendants(shell).OfType<Mixdrome.ErrorDialog.ErrorDialogView>().Single();
                var errorModel = ((ShellViewModel)shell.DataContext).ErrorDialogViewModel;
                var errorChecks = new JsonObject();
                report["errorOverlayChecks"] = errorChecks;
                errorChecks["hiddenAtStartup"] = JsonValue.CreateBooleanValue(errorView.Visibility == Visibility.Collapsed && !errorView.IsEnabled);
                try
                {
                    errorModel.Show();
                    shell.UpdateLayout();
                    errorChecks["enabledWhenShown"] = JsonValue.CreateBooleanValue(errorView.Visibility == Visibility.Visible && errorView.IsEnabled);
                    pages.Add(await Capture(shell, folder, "error-overlay-shown"));
                }
                finally { errorModel.Hide(); shell.UpdateLayout(); }
                errorChecks["disabledWhenHiddenAgain"] = JsonValue.CreateBooleanValue(errorView.Visibility == Visibility.Collapsed && !errorView.IsEnabled);
                await SaveReport(root, folder, report);
                if (shell.ShellFrame.Content is MainView)
                {
                    if (NavigationExtensions.NavigateToViewModel<NavidromeBrowserViewModel>(navigation, "albums"))
                    {
                        pages.Add(await Capture(shell, folder, "albums"));
                        var originalWidth = shell.Width;
                        var originalHeight = shell.Height;
                        try
                        {
                            // Match the app-content viewport in the supplied MixRadio
                            // screenshot, then inspect the supported compact layouts.
                            shell.Width = 1744;
                            shell.Height = 1090;
                            shell.UpdateLayout();
                            pages.Add(await Capture(shell, folder, "albums-reference-size"));
                            var browser = shell.ShellFrame.Content as NavidromeBrowserView;
                            var library = browser == null ? null : browser.FindName("LibraryItems") as GridView;
                            if (library != null && library.Items.Count > 0)
                            {
                                library.SelectedItems.Add(library.Items[0]);
                                var first = library.ContainerFromIndex(0) as Control;
                                if (first != null) first.Focus(FocusState.Keyboard);
                                pages.Add(await Capture(shell, folder, "albums-selected"));
                                library.SelectedItems.Clear();
                                if (first != null) first.Focus(FocusState.Keyboard);
                                pages.Add(await Capture(shell, folder, "albums-focus"));
                                if (first != null)
                                {
                                    VisualStateManager.GoToState(first, "PointerOver", false);
                                    pages.Add(await Capture(shell, folder, "albums-hover-preview"));
                                    VisualStateManager.GoToState(first, "Normal", false);
                                    var artwork = Descendants(first).OfType<Mixdrome.Common.MixRadioArtwork>().FirstOrDefault();
                                    if (artwork != null)
                                    {
                                        var binding = artwork.GetBindingExpression(Mixdrome.Common.MixRadioArtwork.SourceProperty);
                                        var source = artwork.Source;
                                        try
                                        {
                                            artwork.Source = null;
                                            pages.Add(await Capture(shell, folder, "albums-missing-cover-preview"));
                                        }
                                        finally
                                        {
                                            if (binding != null) artwork.SetBinding(Mixdrome.Common.MixRadioArtwork.SourceProperty, binding.ParentBinding);
                                            else artwork.Source = source;
                                        }
                                    }
                                }
                            }
                            foreach (var compactWidth in new[] { 900, 500 })
                            {
                                shell.Width = compactWidth;
                                shell.Height = 900;
                                shell.UpdateLayout();
                                pages.Add(await Capture(shell, folder, "albums-" + compactWidth));
                            }
                            shell.Width = 1744;
                            shell.Height = 1090;
                            shell.UpdateLayout();
                            if (NavigationExtensions.NavigateToViewModel<NavidromeBrowserViewModel>(navigation, "artists"))
                            {
                                pages.Add(await Capture(shell, folder, "artists"));
                                var artists = (NavidromeBrowserView)shell.ShellFrame.Content;
                                var filter = (Button)artists.FindName("CurrentFilter");
                                var peer = new ButtonAutomationPeer(filter);
                                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                                await Task.Delay(500);
                                var popup = VisualTreeHelper.GetOpenPopups(Window.Current).FirstOrDefault();
                                if (popup != null)
                                {
                                    var choices = Descendants(popup.Child).OfType<ListBox>().FirstOrDefault();
                                    report["libraryMenuScreenshot"] = JsonValue.CreateStringValue(await WriteScreenshot(popup.Child, folder, "library-menu"));
                                    report["libraryMenuChoiceCount"] = JsonValue.CreateNumberValue(choices == null ? 0 : choices.Items.Count);
                                    if (choices != null)
                                    {
                                        choices.SelectedItem = choices.Items.OfType<LibraryDestination>().First(destination => destination.Route == "albums");
                                        pages.Add(await Capture(shell, folder, "albums-from-menu"));
                                        var destinationPage = shell.ShellFrame.Content as NavidromeBrowserView;
                                        var destinationModel = destinationPage == null ? null : destinationPage.DataContext as NavidromeBrowserViewModel;
                                        report["libraryMenuNavigated"] = JsonValue.CreateBooleanValue(destinationModel != null && destinationModel.Parameter == "albums");
                                    }
                                }
                            }
                            if (NavigationExtensions.NavigateToViewModel<NavidromeBrowserViewModel>(navigation, "songs"))
                                pages.Add(await Capture(shell, folder, "songs"));
                            var indexChecks = new JsonArray();
                            report["indexChecks"] = indexChecks;
                            foreach (var section in new[] { "albums", "songs", "artists", "genres" })
                            {
                                if (!NavigationExtensions.NavigateToViewModel<NavidromeBrowserViewModel>(navigation, section)) continue;
                                pages.Add(await Capture(shell, folder, section + "-grouped"));
                                var indexedView = (NavidromeBrowserView)shell.ShellFrame.Content;
                                var indexedModel = (NavidromeBrowserViewModel)indexedView.DataContext;
                                var before = indexedModel.Items.Count;
                                var listBeforeIndex = (GridView)indexedView.FindName("LibraryItems");
                                var pagingSelection = listBeforeIndex.Items.OfType<NavidromeItem>().LastOrDefault();
                                var pagingTested = pagingSelection != null && indexedModel.CanLoadMore;
                                if (pagingTested)
                                {
                                    listBeforeIndex.SelectedItems.Add(pagingSelection);
                                    listBeforeIndex.ScrollIntoView(pagingSelection, ScrollIntoViewAlignment.Leading);
                                    await indexedModel.LoadMore();
                                    pages.Add(await Capture(shell, folder, section + "-paged"));
                                }
                                var pagingPreserved = !pagingTested || indexedModel.SelectedItems.Contains(pagingSelection.SelectionItem);
                                listBeforeIndex.SelectedItems.Clear();
                                if (listBeforeIndex.Items.Count > 0) listBeforeIndex.ScrollIntoView(listBeforeIndex.Items[0], ScrollIntoViewAlignment.Leading);
                                listBeforeIndex.UpdateLayout();
                                await indexedView.ShowIndex();
                                pages.Add(await Capture(shell, folder, section + "-index"));
                                var target = indexedModel.IndexGroups.LastOrDefault(group => group.IsEnabled);
                                var check = new JsonObject();
                                check["section"] = JsonValue.CreateStringValue(section);
                                check["pagingTested"] = JsonValue.CreateBooleanValue(pagingTested);
                                check["pagingSelectionPreserved"] = JsonValue.CreateBooleanValue(pagingPreserved);
                                check["beforeCount"] = JsonValue.CreateNumberValue(before);
                                check["afterCount"] = JsonValue.CreateNumberValue(indexedModel.Items.Count);
                                check["complete"] = JsonValue.CreateBooleanValue(!indexedModel.CanLoadMore && !indexedModel.HasError);
                                check["indexVisible"] = JsonValue.CreateBooleanValue(((Grid)indexedView.FindName("IndexSurface")).Visibility == Visibility.Visible);
                                check["groupCount"] = JsonValue.CreateNumberValue(indexedModel.IndexGroups.Count);
                                check["uniqueEntryIdentities"] = JsonValue.CreateBooleanValue(HasUniqueEntryIdentities(listBeforeIndex));
                                if (target != null)
                                {
                                    indexedView.JumpToIndex(target);
                                    pages.Add(await Capture(shell, folder, section + "-index-jump"));
                                    var indexedList = (GridView)indexedView.FindName("LibraryItems");
                                    var targetContainer = indexedList.ContainerFromItem(target) as FrameworkElement;
                                    check["target"] = JsonValue.CreateStringValue(target.Title);
                                    if (targetContainer != null)
                                    {
                                        var point = targetContainer.TransformToVisual(indexedList).TransformPoint(new Point());
                                        check["targetVisible"] = JsonValue.CreateBooleanValue(point.X >= 0 && point.Y >= 0 && point.X < indexedList.ActualWidth && point.Y < indexedList.ActualHeight);
                                    }
                                    indexedList.SelectedItems.Add(target);
                                    check["headerExcludedFromSelection"] = JsonValue.CreateBooleanValue(!indexedModel.SelectedItems.Contains(target) && !indexedList.SelectedItems.Contains(target));
                                    var firstMedia = indexedList.Items.OfType<NavidromeItem>().GroupBy(item => item.SelectionItem).OrderByDescending(group => group.Count()).Select(group => group.First()).FirstOrDefault();
                                    if (firstMedia != null) indexedList.SelectedItems.Add(firstMedia);
                                    await indexedView.ShowIndex();
                                    check["mediaSelectionPreserved"] = JsonValue.CreateBooleanValue(firstMedia == null || indexedModel.SelectedItems.Contains(firstMedia.SelectionItem));
                                    check["sharedOccurrenceSelection"] = JsonValue.CreateBooleanValue(firstMedia == null || indexedList.Items.OfType<NavidromeItem>()
                                        .Where(item => item.SelectionItem == firstMedia.SelectionItem).All(item => indexedList.SelectedItems.Contains(item)));
                                    indexedView.JumpToIndex(target);
                                    if (section == "genres")
                                    {
                                        shell.Width = 1920; shell.Height = 1138; shell.UpdateLayout();
                                        await indexedView.ShowIndex(target);
                                        indexedView.JumpToIndex(target);
                                        pages.Add(await Capture(shell, folder, "genres-jump-original-viewport"));
                                    }
                                    if (firstMedia != null) indexedList.SelectedItems.Remove(firstMedia);
                                    check["sharedOccurrenceDeselection"] = JsonValue.CreateBooleanValue(indexedModel.SelectedItems.Count == 0 && indexedList.SelectedItems.Count == 0);
                                    await indexedView.ShowIndex(target);
                                    shell.Width = 500; shell.Height = 900; shell.UpdateLayout();
                                    pages.Add(await Capture(shell, folder, section + "-index-500"));
                                    shell.Width = 1744; shell.Height = 1090; shell.UpdateLayout();
                                }
                                indexChecks.Add(check);
                                await SaveReport(root, folder, report);
                            }
                        }
                        finally { shell.Width = originalWidth; shell.Height = originalHeight; shell.UpdateLayout(); }
                    }
                    await SaveReport(root, folder, report);
                    if (NavigationExtensions.NavigateToViewModel<NavidromePlayerViewModel>(navigation))
                        pages.Add(await Capture(shell, folder, "player"));
                    await SaveReport(root, folder, report);
                    if (NavigationExtensions.NavigateToViewModel<MainViewModel>(navigation))
                        pages.Add(await Capture(shell, folder, "home"));
                    await SaveReport(root, folder, report);
                    var homeView = shell.ShellFrame.Content as MainView;
                    var hub = homeView == null ? null : homeView.FindName("MainHub") as Hub;
                    if (hub != null)
                    {
                        hub.ScrollToSection(hub.Sections[1]);
                        pages.Add(await Capture(shell, folder, "home-library"));
                        await SaveReport(root, folder, report);
                        hub.ScrollToSection(hub.Sections[0]);
                    }
                    var width = shell.Width;
                    var height = shell.Height;
                    try
                    {
                        shell.Width = 500;
                        shell.Height = 900;
                        shell.UpdateLayout();
                        pages.Add(await Capture(shell, folder, "home-narrow"));
                    }
                    finally { shell.Width = width; shell.Height = height; shell.UpdateLayout(); }
                }
                await SaveReport(root, folder, report);
                App.LogStartupEvent("Native UI probe complete: " + folder.Path);
            }
            catch (Exception exception) { App.LogStartupException("Native UI probe failed", exception); }
#endif
        }

#if NATIVE_UI_OBSERVE
        // Passive observation only. Requests name snapshots; they cannot invoke
        // navigation, focus, selection, sizing or any product command.
        private static async Task ObserveAsync(ShellView shell)
        {
            var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync("native-ui-observer", CreationCollisionOption.OpenIfExists);
            var folder = await root.CreateFolderAsync(DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"), CreationCollisionOption.GenerateUniqueName);
            string previous = null;
            while (true)
            {
                try
                {
                    var request = await root.TryGetItemAsync("request.txt") as StorageFile;
                    var name = request == null ? "startup" : (await FileIO.ReadTextAsync(request)).Trim();
                    if (name != previous && name.Length > 0 && name.Length < 80 && name.All(character => char.IsLetterOrDigit(character) || character == '-'))
                    {
                        var pending = new JsonObject {
                            ["name"] = JsonValue.CreateStringValue(name),
                            ["directory"] = JsonValue.CreateStringValue(folder.Path),
                            ["observedAt"] = JsonValue.CreateStringValue(DateTimeOffset.Now.ToString("O")),
                            ["focus"] = Describe(Windows.UI.Xaml.Input.FocusManager.GetFocusedElement() as FrameworkElement)
                        };
                        var currentBrowser = shell.ShellFrame.Content as NavidromeBrowserView;
                        if (currentBrowser != null)
                        {
                            pending["indexVisible"] = JsonValue.CreateBooleanValue(((Grid)currentBrowser.FindName("IndexSurface")).Visibility == Visibility.Visible);
                            pending["route"] = JsonValue.CreateStringValue(((NavidromeBrowserViewModel)currentBrowser.DataContext).Parameter);
                        }
                        await PublishObservation(root, "pending", pending);
                        var report = await Capture(shell, folder, name, false);
                        report["observedAt"] = JsonValue.CreateStringValue(DateTimeOffset.Now.ToString("O"));
                        report["mode"] = JsonValue.CreateStringValue("passive-no-injected-actions");
                        report["directory"] = JsonValue.CreateStringValue(folder.Path);
                        var focus = Windows.UI.Xaml.Input.FocusManager.GetFocusedElement() as FrameworkElement;
                        report["focus"] = Describe(focus);
                        var ancestors = new JsonArray();
                        for (DependencyObject ancestor = focus; ancestor != null; ancestor = VisualTreeHelper.GetParent(ancestor))
                        {
                            var element = ancestor as FrameworkElement;
                            if (element != null) ancestors.Add(Describe(element));
                        }
                        report["focusAncestors"] = ancestors;
                        var controls = new JsonArray();
                        foreach (var control in Descendants(shell).OfType<Control>().Where(control => control.Visibility == Visibility.Visible && control.ActualWidth > 0 &&
                            (control is Button || control is GridViewItem || control is ListBoxItem)))
                        {
                            var item = Describe(control); item["bounds"] = Bounds(control, shell); controls.Add(item);
                        }
                        report["controls"] = controls;
                        var popups = new JsonArray();
                        foreach (var popup in VisualTreeHelper.GetOpenPopups(Window.Current).Where(popup => popup.IsOpen))
                        {
                            var item = new JsonObject();
                            item["screenshot"] = JsonValue.CreateStringValue(await WriteScreenshot(popup.Child, folder, name + "-popup-" + popups.Count));
                            var choices = new JsonArray();
                            foreach (var control in Descendants(popup.Child).OfType<Control>().Where(control => control is ListBoxItem || control is Button)) choices.Add(Describe(control));
                            item["controls"] = choices; popups.Add(item);
                        }
                        report["popups"] = popups;
                        var browser = shell.ShellFrame.Content as NavidromeBrowserView;
                        var model = browser == null ? null : browser.DataContext as NavidromeBrowserViewModel;
                        if (model != null)
                        {
                            report["route"] = JsonValue.CreateStringValue(model.Parameter ?? string.Empty);
                            report["indexVisible"] = JsonValue.CreateBooleanValue(((Grid)browser.FindName("IndexSurface")).Visibility == Visibility.Visible);
                            report["mediaCount"] = JsonValue.CreateNumberValue(model.Items.Count);
                            report["selectedMediaCount"] = JsonValue.CreateNumberValue(model.SelectedItems.Count);
                        }
                        // Publish only completed JSON. Polling readers must never
                        // open an empty destination between CreateFile and WriteText.
                        await PublishObservation(folder, name, report);
                        previous = name;
                        await PublishObservation(root, "latest", report);
                    }
                }
                catch (Exception exception) { App.LogStartupException("Passive UI observation failed", exception); }
                await Task.Delay(250);
            }
        }

        private static JsonObject Describe(FrameworkElement element)
        {
            var control = element as Control;
            var content = element as ContentControl;
            var group = element == null ? null : element.DataContext as LibraryIndexGroup;
            var media = element == null ? null : element.DataContext as NavidromeItem;
            var destination = element == null ? null : element.DataContext as LibraryDestination;
            return new JsonObject {
                ["type"] = JsonValue.CreateStringValue(element == null ? string.Empty : element.GetType().Name),
                ["name"] = JsonValue.CreateStringValue(element == null ? string.Empty : element.Name),
                ["visibility"] = JsonValue.CreateStringValue(element == null ? string.Empty : element.Visibility.ToString()),
                ["dataContext"] = JsonValue.CreateStringValue(element == null || element.DataContext == null ? string.Empty : element.DataContext.GetType().FullName),
                ["label"] = JsonValue.CreateStringValue(group != null ? group.Title : media != null ? media.Title ?? string.Empty : destination != null ? destination.Label : content != null && content.Content is string ? (string)content.Content : string.Empty),
                ["enabled"] = JsonValue.CreateBooleanValue(control != null && control.IsEnabled),
                ["focusState"] = JsonValue.CreateStringValue(control == null ? string.Empty : control.FocusState.ToString())
            };
        }
        private static async Task PublishObservation(StorageFolder folder, string name, JsonObject report)
        {
            var completed = await folder.CreateFileAsync(name + ".writing", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(completed, report.Stringify());
            await completed.RenameAsync(name + ".json", NameCollisionOption.ReplaceExisting);
        }
#endif

        private static bool HasUniqueEntryIdentities(GridView list)
        {
            return list.Items.Cast<object>().Distinct().Count() == list.Items.Count;
        }

        private static async Task SaveReport(StorageFolder root, StorageFolder folder, JsonObject report)
        {
            var text = report.Stringify();
            await FileIO.WriteTextAsync(await folder.CreateFileAsync("result.json", CreationCollisionOption.ReplaceExisting), text);
            await FileIO.WriteTextAsync(await root.CreateFileAsync("latest.json", CreationCollisionOption.ReplaceExisting), text);
        }

        private static async Task<JsonObject> Capture(ShellView shell, StorageFolder folder, string name, bool settle = true)
        {
            var page = shell.ShellFrame.Content as FrameworkElement;
            var model = page == null ? null : page.DataContext as NavidromePageViewModel;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (model != null && model.IsLoading && DateTimeOffset.UtcNow < deadline) await Task.Delay(100);
            await Task.Delay(500);
            shell.UpdateLayout();
            var artworkDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (settle && RemoteImages(shell).Any(bitmap => bitmap.PixelWidth == 0) && DateTimeOffset.UtcNow < artworkDeadline)
                await Task.Delay(100);
            // Include the recovered 600 ms fade after the last image finishes decoding.
            await Task.Delay(700);
            var result = new JsonObject();
            result["name"] = JsonValue.CreateStringValue(name);
            result["page"] = JsonValue.CreateStringValue(page == null ? string.Empty : page.GetType().FullName);
            result["loading"] = JsonValue.CreateBooleanValue(model != null && model.IsLoading);
            result["hasError"] = JsonValue.CreateBooleanValue(model != null && model.HasError);
            result["width"] = JsonValue.CreateNumberValue(shell.ActualWidth);
            result["height"] = JsonValue.CreateNumberValue(shell.ActualHeight);
            result["initializing"] = JsonValue.CreateBooleanValue(((ShellViewModel)shell.DataContext).IsInitializing);
            result["artworkDecoded"] = JsonValue.CreateNumberValue(RemoteImages(shell).Count(bitmap => bitmap.PixelWidth > 0));
            result["artworkPendingOrUnavailable"] = JsonValue.CreateNumberValue(RemoteImages(shell).Count(bitmap => bitmap.PixelWidth == 0));
            var header = new JsonObject();
            foreach (var elementName in new[] { "GoBack", "PageTitle", "CurrentFilter", "SearchControl" })
            {
                var element = page == null ? null : page.FindName(elementName) as FrameworkElement;
                if (element != null && element.Visibility == Visibility.Visible) header[elementName] = Bounds(element, page);
            }
            result["header"] = header;
            var lists = new JsonArray();
            foreach (var list in Descendants(shell).OfType<GridView>().Where(list => list.Visibility == Visibility.Visible))
            {
                var layout = new JsonObject();
                layout["name"] = JsonValue.CreateStringValue(list.Name);
                layout["width"] = JsonValue.CreateNumberValue(list.ActualWidth);
                layout["height"] = JsonValue.CreateNumberValue(list.ActualHeight);
                layout["padding"] = JsonValue.CreateStringValue(list.Padding.ToString());
                layout["count"] = JsonValue.CreateNumberValue(list.Items.Count);
                var panel = list.ItemsPanelRoot;
                if (panel != null)
                {
                    layout["panelWidth"] = JsonValue.CreateNumberValue(panel.ActualWidth);
                    layout["panelHeight"] = JsonValue.CreateNumberValue(panel.ActualHeight);
                }
                var bounds = new JsonArray();
                for (var index = 0; index < list.Items.Count; index++)
                {
                    var item = list.ContainerFromIndex(index) as FrameworkElement;
                    if (item == null) continue;
                    var point = item.TransformToVisual(list).TransformPoint(new Point());
                    var box = new JsonObject();
                    box["index"] = JsonValue.CreateNumberValue(index);
                    box["x"] = JsonValue.CreateNumberValue(point.X);
                    box["y"] = JsonValue.CreateNumberValue(point.Y);
                    box["width"] = JsonValue.CreateNumberValue(item.ActualWidth);
                    box["height"] = JsonValue.CreateNumberValue(item.ActualHeight);
                    var control = item as Control;
                    if (control != null) box["focusState"] = JsonValue.CreateStringValue(control.FocusState.ToString());
                    var focusVisual = Descendants(item).OfType<FrameworkElement>().FirstOrDefault(element => element.Name == "FocusVisualWhite");
                    if (focusVisual != null) box["focusVisualOpacity"] = JsonValue.CreateNumberValue(focusVisual.Opacity);
                    var media = list.Items[index] as NavidromeItem;
                    var card = Descendants(item).OfType<Grid>().FirstOrDefault(grid => Math.Abs(grid.ActualWidth - 260) < 0.1 && Math.Abs(grid.ActualHeight - 130) < 0.1);
                    if (card != null && media != null)
                    {
                        box["card"] = Bounds(card, page);
                        var texts = new JsonArray();
                        foreach (var block in Descendants(card).OfType<TextBlock>().Where(block => !string.IsNullOrEmpty(block.Text)))
                        {
                            var textBounds = Bounds(block, card);
                            var position = block.TransformToVisual(card).TransformPoint(new Point());
                            textBounds["isCount"] = JsonValue.CreateBooleanValue(block.Text == media.CountText);
                            textBounds["withinCard"] = JsonValue.CreateBooleanValue(position.X >= 0 && position.Y >= 0 &&
                                position.X + block.ActualWidth <= card.ActualWidth + 0.1 && position.Y + block.ActualHeight <= card.ActualHeight + 0.1);
                            texts.Add(textBounds);
                        }
                        box["texts"] = texts;
                    }
                    bounds.Add(box);
                }
                layout["items"] = bounds;
                lists.Add(layout);
            }
            result["lists"] = lists;
            var home = model as MainViewModel;
            if (home != null)
            {
                var sections = new JsonArray();
                foreach (var section in new[] { home.Recent, home.Playlists, home.Artists, home.Downloads, home.Favourites, home.MyMusic })
                {
                    var item = new JsonObject();
                    item["route"] = JsonValue.CreateStringValue(section.Destination.Route);
                    item["count"] = JsonValue.CreateNumberValue(section.Items.Count);
                    item["loading"] = JsonValue.CreateBooleanValue(section.IsLoading);
                    item["hasError"] = JsonValue.CreateBooleanValue(!string.IsNullOrEmpty(section.Error));
                    sections.Add(item);
                }
                result["sections"] = sections;
            }
            try
            {
                result["screenshot"] = JsonValue.CreateStringValue(await WriteScreenshot(shell, folder, name));
            }
            catch (Exception exception) { result["captureError"] = JsonValue.CreateStringValue(exception.ToString()); }
            return result;
        }

        private static JsonObject Bounds(FrameworkElement element, UIElement relativeTo)
        {
            var point = element.TransformToVisual(relativeTo).TransformPoint(new Point());
            return new JsonObject {
                ["x"] = JsonValue.CreateNumberValue(point.X), ["y"] = JsonValue.CreateNumberValue(point.Y),
                ["width"] = JsonValue.CreateNumberValue(element.ActualWidth), ["height"] = JsonValue.CreateNumberValue(element.ActualHeight)
            };
        }

        private static async Task<string> WriteScreenshot(UIElement element, StorageFolder folder, string name)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(element);
            var pixels = await bitmap.GetPixelsAsync();
            var filename = name + ".png";
            var file = await folder.CreateFileAsync(filename, CreationCollisionOption.FailIfExists);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
                await encoder.FlushAsync();
            }
            return filename;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private static IEnumerable<BitmapImage> RemoteImages(DependencyObject root)
        {
            return Descendants(root).OfType<Image>().Select(control => control.Source).OfType<BitmapImage>()
                .Where(bitmap => bitmap.UriSource != null &&
                    (bitmap.UriSource.Scheme == "http" || bitmap.UriSource.Scheme == "https"));
        }
    }
}
