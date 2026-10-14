using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using SCSKiller.Core;

namespace SCSKiller.App.Pages;

public sealed partial class LibraryPage : Page
{
    public LibraryVm Vm { get; } = new();

    public LibraryPage()
    {
        InitializeComponent();
        ((CollectionViewSource)Resources["GroupedGames"]).Source = Vm.Groups;
        var refresh = new Coalesced(DispatcherQueue, Vm.Refresh);
        void OnChanged(GameState _) => refresh.Request();
        void OnQueue(QueueItem q) { if (q.Stage != QueueStage.Warming) refresh.Request(); }   // progress ticks change nothing here
        Loaded += (_, _) => { App.Core.GameChanged += OnChanged; App.Core.QueueChanged += OnQueue; Icons.Failed += refresh.Request; Vm.Load(); };
        Unloaded += (_, _) => { App.Core.GameChanged -= OnChanged; App.Core.QueueChanged -= OnQueue; Icons.Failed -= refresh.Request; };
    }

    void OnSearch(AutoSuggestBox box, AutoSuggestBoxTextChangedEventArgs _) => Vm.Filter = box.Text;

    void OnSearchKey(object _, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape || Search.Text.Length == 0) return;
        Search.Text = "";
        e.Handled = true;
    }

    /// <summary>--screenshots: a search typed, and the list scrolled to its last section.</summary>
    public string SearchText { set => Search.Text = value; }
    public void ScrollToEnd() { if (Vm.Games.Count > 0) List.ScrollIntoView(Vm.Groups[^1][^1]); }

    static GameRow RowOf(object sender) => (GameRow)((FrameworkElement)sender).DataContext;

    void OnGameClick(object _, ItemClickEventArgs e) => App.Main.Navigate(typeof(DetailPage), ((GameRow)e.ClickedItem).Id);
    void OnDetails(object sender, RoutedEventArgs _) => App.Main.Navigate(typeof(DetailPage), RowOf(sender).Id);
    void OnSettings(object _, RoutedEventArgs __) => App.Main.Navigate(typeof(SettingsPage));
    void OnPlay(object sender, RoutedEventArgs _) => Vm.Play(RowOf(sender));

    // Either click fetches the server's data whatever its age; Shift+click also forces re-detection, which is otherwise
    // redone only when an exe, build or SCSKiller changes.
    void OnRefresh(object _, RoutedEventArgs __) =>
        Vm.Rescan(force: Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down), userRequested: true);

    // Adding only queues the game: nothing compiles until Compile queue.
    void OnAdd(object sender, RoutedEventArgs _)
    {
        App.Core.Enqueue(RowOf(sender).Id);
        Vm.Refresh();
    }

    void OnDismissGpuNotice(InfoBar _, object __) => Vm.DismissGpuNotice();
    // A launcher stub is resolved to the game's own exe, shown with the game folder to confirm; an exe of a listed game
    // opens that game instead.
    async void OnAddGame(object _, RoutedEventArgs __)
    {
        string? path;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.Main.AppWindow.Id)
            {
                SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.ComputerFolder, CommitButtonText = "添加游戏",
            };
            picker.FileTypeFilter.Add(".exe");
            path = (await picker.PickSingleFileAsync())?.Path;
        }
        catch (Exception ex) { await Message("无法打开文件选择器", ex.Message); return; }
        if (path == null) return;
        ManualAdd added;
        try { added = await Task.Run(() => App.Core.PreviewManualGame(path)); }   // reads the install's files
        catch (Exception ex)   // a rejection's message, or a data folder SCSKiller couldn't read
        {
            await Message("无法添加此游戏", ex.Message);
            return;
        }
        if (!added.Existed)
        {
            if (await GameFolderDialog.ShowAsync(XamlRoot, added.Game, $"添加 {added.Game.Name}", "添加游戏") is not { } folder) return;
            try { await Task.Run(() => App.Core.AddManualGame(path, folder)); }
            catch (Exception ex)
            {
                await Message("无法添加此游戏", ex.Message);
                return;
            }
            Vm.Rescan(force: false);   // lists it under "Added by you" once its engine and anti-cheat are checked
            return;
        }
        var listed = App.Core.Games.Any(s => s.Game.Id == added.Game.Id);
        var where = added.Game.Store == Store.Manual ? "你已添加过它" : $"SCSKiller 已在 {Fmt.StoreName(added.Game)} 找到它";
        if (listed && await App.ConfirmAsync(this, "游戏已在库中", $"{added.Game.Name} 已在列表中：{where}。", "打开详情", ContentDialogButton.Primary))
            App.Main.Navigate(typeof(DetailPage), added.Game.Id);
        else if (!listed) await Message("游戏已在库中", $"{added.Game.Name}：{where}。刷新游戏库后即可看到。");
    }

    Task Message(string title, string text) => App.ShowAsync(new ContentDialog
    {
        XamlRoot = XamlRoot, Title = title, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "确定",
    });

    void OnAddAll(object _, RoutedEventArgs __) => Vm.AddAllReady();
    void OnAddRecommended(object _, RoutedEventArgs __) => Vm.AddAllRecommended();

    void OnCompileQueue(object _, RoutedEventArgs __)
    {
        App.CompileQueue();
        App.Main.Navigate(typeof(QueuePage));
    }

    // Unsupported: say why; an encrypted game can be unlocked with its AES key (stored on this PC only).
    async void OnWhy(object sender, RoutedEventArgs _)
    {
        var row = RowOf(sender);
        var text = new TextBlock { Text = row.Reason, TextWrapping = TextWrapping.Wrap };
        var box = new TextBox { PlaceholderText = "0x 后接 64 位十六进制数字", Header = "AES 密钥", Visibility = row.IsEncrypted ? Visibility.Visible : Visibility.Collapsed };
        var panel = new StackPanel { Spacing = 12, Children = { text, box } };
        if (row.IsEncrypted)
            panel.Children.Insert(1, new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "SCSKiller 未能在此游戏的执行文件中找到密钥（受保护的执行文件可能隐藏它）。如果你有密钥，请粘贴在这里；"
                + "程序会用游戏文件验证，并且只保存在本机。" });
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = $"为什么无法编译 {row.Name}？", Content = panel, CloseButtonText = "关闭",
            PrimaryButtonText = row.IsEncrypted ? "解锁" : "", DefaultButton = ContentDialogButton.Close,
        };
        if (await App.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        bool ok;
        var key = box.Text.Trim();
        try { ok = await Task.Run(() => App.Core.SetEncryptionKey(row.Id, key)); }   // opens the game's files
        catch (Exception ex) { ok = false; text.Text = ex.Message; }
        if (ok) Vm.Rescan();
        else await App.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = "密钥无效", Content = "此密钥无法打开游戏文件。", CloseButtonText = "确定" });
    }

    async void OnRecord(object sender, RoutedEventArgs _)
    {
        var row = RowOf(sender);
        if (!await App.ConfirmAsync(this, $"为 {row.Name} 开启录制？",
                "SCSKiller 会在游戏旁安装一个小型 d3d12.dll，记录游戏创建的管线。游玩约 5 分钟后关闭游戏，"
                + "再把它加入编译队列。你可以随时在游戏详情页卸载录制器。"
                + (row.State.AntiCheat != AntiCheat.None ? " 已检测到反作弊：注入仍可能被阻止或导致封禁。" : ""),
                "安装录制器", ContentDialogButton.Primary)) return;
        try { await Task.Run(() => App.Core.InstallRecorder(row.Id)); }   // file IO and the game's re-evaluation
        catch (Exception ex) { await App.ShowAsync(new ContentDialog { XamlRoot = XamlRoot, Title = "无法安装录制器", Content = ex.Message, CloseButtonText = "确定" }); }
        Vm.Refresh();
    }
}
