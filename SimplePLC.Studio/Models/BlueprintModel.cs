using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Models;

public partial class BlueprintModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _titleVi = string.Empty;

    [ObservableProperty]
    private string _titleEn = string.Empty;

    [ObservableProperty]
    private string _subtitleVi = string.Empty;

    [ObservableProperty]
    private string _subtitleEn = string.Empty;

    [ObservableProperty]
    private string _descriptionVi = string.Empty;

    [ObservableProperty]
    private string _descriptionEn = string.Empty;

    [ObservableProperty]
    private string _badgeVi = string.Empty;

    [ObservableProperty]
    private string _badgeEn = string.Empty;

    [ObservableProperty]
    private string _badgeColor = "#107C41";

    [ObservableProperty]
    private string _icon = "⚡";

    [ObservableProperty]
    private int _ruleCount;

    [ObservableProperty]
    private string _tagsSummaryVi = string.Empty;

    [ObservableProperty]
    private string _tagsSummaryEn = string.Empty;

    public string Title
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(TitleVi) ? TitleVi : TitleEn) 
            : (!string.IsNullOrWhiteSpace(TitleEn) ? TitleEn : TitleVi);
        set
        {
            TitleVi = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Subtitle
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(SubtitleVi) ? SubtitleVi : SubtitleEn) 
            : (!string.IsNullOrWhiteSpace(SubtitleEn) ? SubtitleEn : SubtitleVi);
        set
        {
            SubtitleVi = value;
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public string Description
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(DescriptionVi) ? DescriptionVi : DescriptionEn) 
            : (!string.IsNullOrWhiteSpace(DescriptionEn) ? DescriptionEn : DescriptionVi);
        set
        {
            DescriptionVi = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public string Badge
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(BadgeVi) ? BadgeVi : BadgeEn) 
            : (!string.IsNullOrWhiteSpace(BadgeEn) ? BadgeEn : BadgeVi);
        set
        {
            BadgeVi = value;
            OnPropertyChanged(nameof(Badge));
        }
    }

    public string TagsSummary
    {
        get => LocalizationService.Instance.IsVietnamese 
            ? (!string.IsNullOrWhiteSpace(TagsSummaryVi) ? TagsSummaryVi : TagsSummaryEn) 
            : (!string.IsNullOrWhiteSpace(TagsSummaryEn) ? TagsSummaryEn : TagsSummaryVi);
        set
        {
            TagsSummaryVi = value;
            OnPropertyChanged(nameof(TagsSummary));
        }
    }

    public BlueprintModel()
    {
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(TagsSummary));
    }

    public void Dispose()
    {
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
