using System.Collections.ObjectModel;

namespace GalaXako.Editor.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private object? _currentPage;
    private NavigationItem? _selectedNavigationItem;
    private string _notificationMessage = string.Empty;
    private bool _hasNotification;

    public MainWindowViewModel(Func<string?, Task> openFile, Func<string, string?> saveAsPicker, Func<IReadOnlyList<string>> pickMultipleFiles, Func<string?> pickSecondFile, Action<GalaXako.Editor.Core.Models.RecentFile> removeRecent)
    {
        Home = new HomeViewModel(openFile, removeRecent);
        Editor = new EditorViewModel(new GalaXako.Editor.Core.IO.TextFileService(), saveAsPicker);
        Jobs = new JobsViewModel();
        Settings = new SettingsViewModel(App.SettingsStore);
        var pipelineStore = new GalaXako.Editor.Infrastructure.Storage.JsonPipelineStore();
        NavigationItems =
        [
            new("home", "Ana Sayfa", "\uE80F", Home),
            new("editor", "Editör", "\uE70F", Editor),
            new("clean", "Temizle", "\uE74D", Tool("clean", "Temizle", "Satır tabanlı verileri güvenli ve akışlı biçimde normalleştirin.", ["Boşlukları kırp", "Boş satırları kaldır", "Tekrarlanan satırları kaldır", "Satır sonlarını normalleştir"])),
            new("filter", "Filtrele", "\uE71C", Tool("filter", "Filtrele", "AND/OR koşullarıyla güçlü satır filtreleri oluşturun.", ["Metin koşulları", "Regex", "Uzunluk kuralları", "Örnek önizleme"])),
            new("extract", "Ayıkla", "\uE8B7", Tool("extract", "Ayıkla", "Yerel metinden biçim tanımlı değerleri çıkarın.", ["URL ve alan adı", "E-posta biçimi", "IP adresleri", "Hash biçimleri", "Özel regex"])),
            new("delimiter", "Sütunlar", "\uE8EA", Tool("delimiter", "Sütunlar", "Ayraç tabanlı verilerde sütunları genel amaçlı olarak dönüştürün.", ["Özel ayraç", "Sütun çıkar/kaldır", "Yeniden sırala", "Birleştir", "Sütuna göre filtrele"])),
            new("sort", "Sırala", "\uE8CB", Tool("sort", "Sırala", "Küçük ve büyük dosyaları seçilen düzende sıralayın.", ["A-Z / Z-A", "Sayısal", "Uzunluğa göre", "Doğal sıralama"])),
            new("splitmerge", "Böl & Birleştir", "\uE8B0", Tool("splitmerge", "Böl & Birleştir", "Dosyaları belleğe bütünüyle almadan bölün veya birleştirin.", ["Satır sayısına göre böl", "Yaklaşık boyuta göre böl", "Regex sınırları", "Sıralı birleştirme"])),
            new("compare", "Karşılaştır", "\uE8D4", Tool("compare", "Karşılaştır", "İki satır tabanlı dosyanın kümelerini karşılaştırın.", ["Yalnız A", "Yalnız B", "Ortak satırlar", "Farklar"])),
            new("pipeline", "Pipeline", "\uE9D2", Tool("pipeline", "Pipeline", "Yeniden kullanılabilir işlem zincirleri oluşturun.", ["Adım ekle", "Sırala", "JSON preset", "Batch işleme"])),
            new("jobs", "İşler", "\uE9F5", Jobs),
            new("settings", "Ayarlar", "\uE713", Settings)
        ];

        NavigateCommand = new RelayCommand(item => SelectedNavigationItem = item as NavigationItem);
        DismissNotificationCommand = new RelayCommand(_ => HasNotification = false);
        SelectedNavigationItem = NavigationItems[0];

        ToolPageViewModel Tool(string key, string title, string description, IEnumerable<string> capabilities) =>
            new(key, title, description, capabilities, () => Editor.File, Jobs, pickMultipleFiles, pickSecondFile, pipelineStore, () => Settings.MaxConcurrentJobs, () => Settings.Settings);
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public EditorViewModel Editor { get; }
    public JobsViewModel Jobs { get; }
    public HomeViewModel Home { get; }
    public SettingsViewModel Settings { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand DismissNotificationCommand { get; }
    public RelayCommand MinimizeCommand { get; set; } = null!;
    public RelayCommand MaximizeCommand { get; set; } = null!;
    public RelayCommand CloseCommand { get; set; } = null!;
    public string NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public bool HasNotification
    {
        get => _hasNotification;
        private set => SetProperty(ref _hasNotification, value);
    }
    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (value is null || !SetProperty(ref _selectedNavigationItem, value))
            {
                return;
            }

            foreach (var item in NavigationItems)
            {
                item.IsSelected = ReferenceEquals(item, value);
            }

            CurrentPage = value.Page;
            if (value.Page is ToolPageViewModel tool) tool.Refresh();
        }
    }

    public void NavigateTo(string key)
    {
        SelectedNavigationItem = NavigationItems.First(item => item.Key == key);
        if (SelectedNavigationItem.Page is ToolPageViewModel tool) tool.Refresh();
    }

    public void ShowNotification(string message)
    {
        NotificationMessage = message;
        HasNotification = true;
    }
}
