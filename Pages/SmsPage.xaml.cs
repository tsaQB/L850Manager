using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using L850Manager.Models;
namespace L850Manager.Pages;
public sealed partial class SmsPage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _refreshing;
    private IReadOnlyList<SmsMessage> _allMessages = [];
    private string _searchText = string.Empty;

    public SmsPage()
    {
        InitializeComponent();
        Loaded += SmsPage_Loaded;
        Unloaded += (_, _) => _timer.Stop();
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    private async void SmsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Interval = TimeSpan.FromSeconds(App.SmsPollingSeconds);
        _timer.Start();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            int? selectedIndex = (SmsList.SelectedItem as SmsMessage)?.Index;
            _allMessages = await App.Sms.ListAsync();
            IReadOnlyList<SmsMessage> messages = ApplyFilter();
            SmsList.ItemsSource = messages;
            MessageCountText.Text = $"{messages.Count} message{(messages.Count == 1 ? "" : "s")} · Updated {DateTime.Now:HH:mm:ss}";
            SmsMessage? selected = messages.FirstOrDefault(x => x.Index == selectedIndex) ?? messages.FirstOrDefault();
            if (selected is not null) SmsList.SelectedItem = selected;
        }
        catch (Exception ex)
        {
            MessageCountText.Text = ex.Message;
        }
        finally { _refreshing = false; }
    }

    private void SmsFilter_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!IsLoaded) return;
        var filtered = ApplyFilter();
        SmsList.ItemsSource = filtered;
        MessageCountText.Text = $"{filtered.Count} message{(filtered.Count == 1 ? "" : "s")}";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        if (!IsLoaded) return;
        var filtered = ApplyFilter();
        SmsList.ItemsSource = filtered;
        MessageCountText.Text = $"{filtered.Count} message{(filtered.Count == 1 ? "" : "s")}";
    }

    private IReadOnlyList<SmsMessage> ApplyFilter()
    {
        string tag = (SmsFilter.SelectedItem as SelectorBarItem)?.Tag?.ToString() ?? "inbox";
        IEnumerable<SmsMessage> filtered = tag switch
        {
            "sent" => _allMessages.Where(x => x.IsSent),
            "all" => _allMessages,
            _ => _allMessages.Where(x => !x.IsSent)
        };
        if (!string.IsNullOrWhiteSpace(_searchText)) filtered = filtered.Where(x => x.Sender.Contains(_searchText, StringComparison.OrdinalIgnoreCase) || x.Body.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        return filtered.ToArray();
    }

    private void SmsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmsList.SelectedItem is not SmsMessage message) return;
        ConversationTitleText.Text = message.DisplaySender;
        ConversationBodyText.Text = message.Body;
        ConversationDateText.Text = message.Timestamp?.ToLocalTime().ToString("dddd, dd MMMM yyyy") ?? "Unknown date";
        ConversationTimeText.Text = message.DisplayTime;
    }

    private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        CharacterCountText.Text = $"{MessageTextBox.Text.Length} / 160";

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (SmsList.SelectedItem is not SmsMessage selected || string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;
        if (!selected.Sender.All(c => char.IsDigit(c) || c == '+'))
        {
            await ShowInfoAsync("Cannot reply", "This sender does not expose a dialable phone number.");
            return;
        }
        await ConfirmAndSendAsync(selected.Sender, MessageTextBox.Text);
    }

    private async void NewMessage_Click(object sender, RoutedEventArgs e)
    {
        TextBox number = new() { Header = "Recipient", PlaceholderText = "+628…" };
        TextBox body = new() { Header = "Message", AcceptsReturn = true, MaxLength = 160, Height = 110, TextWrapping = TextWrapping.Wrap };
        ContentDialog dialog = new() { XamlRoot = XamlRoot, Title = "New message", Content = new StackPanel { Spacing = 12, Children = { number, body } }, PrimaryButtonText = "Review", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await ConfirmAndSendAsync(number.Text.Trim(), body.Text);
    }

    private async Task ConfirmAndSendAsync(string recipient, string body)
    {
        ContentDialog confirm = new() { XamlRoot = XamlRoot, Title = "Send SMS?", Content = $"Recipient: {recipient}\n\n{body}", PrimaryButtonText = "Send", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            SmsMessage sent = await App.Sms.SendAsync(recipient, body);
            MessageTextBox.Text = string.Empty;
            await RefreshAsync();
            SmsFilter.SelectedItem = SmsFilter.Items.OfType<SelectorBarItem>().First(x => Equals(x.Tag, "sent"));
            SmsList.SelectedItem = (SmsList.ItemsSource as IEnumerable<SmsMessage>)?.FirstOrDefault(x => x.Index == sent.Index);
            await ShowInfoAsync("Message sent", "The modem accepted the SMS for network delivery and it now appears in Sent.");
        }
        catch (Exception ex) { await ShowInfoAsync("Send failed", ex.Message); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SmsList.SelectedItem is not SmsMessage selected) return;
        ContentDialog confirm = new() { XamlRoot = XamlRoot, Title = "Delete this SMS?", Content = "This removes the message from SIM storage and cannot be undone.", PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        try { await App.Sms.DeleteAsync(selected.Index); await RefreshAsync(); }
        catch (Exception ex) { await ShowInfoAsync("Delete failed", ex.Message); }
    }

    private async Task ShowInfoAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "Close" }.ShowAsync();
}
