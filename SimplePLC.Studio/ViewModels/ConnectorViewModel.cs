using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public partial class ConnectorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isInput;

    [ObservableProperty]
    private bool _isEvent;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private Point _anchor;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    private string _description = string.Empty;
    public string Description
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_description)) return _description;
            if (!string.IsNullOrWhiteSpace(TooltipKey)) return LocalizationService.Instance.Get(TooltipKey);
            return LocalizationService.Instance.IsVietnamese ? DescriptionVi : DescriptionEn;
        }
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(TooltipText));
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string _tooltipKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string _descriptionVi = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TooltipText))]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string _descriptionEn = string.Empty;

    public ConnectorViewModel()
    {
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(TooltipText));
    }

    public string TooltipText
    {
        get
        {
            string desc = !string.IsNullOrWhiteSpace(TooltipKey)
                ? LocalizationService.Instance.Get(TooltipKey)
                : (LocalizationService.Instance.IsVietnamese ? DescriptionVi : DescriptionEn);

            if (string.IsNullOrWhiteSpace(desc))
            {
                desc = Description;
            }

            return !string.IsNullOrWhiteSpace(desc)
                ? $"{Title}: {desc}"
                : Title;
        }
    }

    public void SetBilingualDescription(string vi, string en)
    {
        DescriptionVi = vi;
        DescriptionEn = en;
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(TooltipText));
    }

    public void ClearDiagnostics()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    public GraphNodeViewModel Node { get; set; } = null!;
}
