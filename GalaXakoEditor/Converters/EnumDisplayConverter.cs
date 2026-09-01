using System.Globalization;
using System.Windows.Data;
using GalaXako.Editor.Core.Operations;
using GalaXako.Editor.Core.Pipeline;

namespace GalaXako.Editor.App.Converters;

public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TextCaseTransform.None => "Değiştirme",
        TextCaseTransform.Lowercase => "küçük harf",
        TextCaseTransform.Uppercase => "BÜYÜK HARF",

        FilterLogic.And => "Tüm kurallar (VE)",
        FilterLogic.Or => "Herhangi bir kural (VEYA)",
        FilterCondition.Contains => "İçerir",
        FilterCondition.DoesNotContain => "İçermez",
        FilterCondition.StartsWith => "İle başlar",
        FilterCondition.EndsWith => "İle biter",
        FilterCondition.Equals => "Eşittir",
        FilterCondition.DoesNotEqual => "Eşit değildir",
        FilterCondition.RegexMatches => "Regex eşleşir",
        FilterCondition.RegexDoesNotMatch => "Regex eşleşmez",
        FilterCondition.LengthGreaterThan => "Uzunluğu büyüktür",
        FilterCondition.LengthLessThan => "Uzunluğu küçüktür",
        FilterCondition.LengthBetween => "Uzunluğu aralıktadır",

        ExtractorKind.Url => "URL",
        ExtractorKind.Domain => "Alan adı",
        ExtractorKind.Email => "E-posta",
        ExtractorKind.IPv4 => "IPv4",
        ExtractorKind.IPv6 => "IPv6",
        ExtractorKind.Md5 => "MD5",
        ExtractorKind.Sha1 => "SHA-1",
        ExtractorKind.Sha256 => "SHA-256",
        ExtractorKind.CustomRegex => "Özel regex",

        SortMode.AlphabeticalAscending => "A-Z",
        SortMode.AlphabeticalDescending => "Z-A",
        SortMode.ShortestFirst => "En kısa önce",
        SortMode.LongestFirst => "En uzun önce",
        SortMode.NumericAscending => "Sayısal artan",
        SortMode.NumericDescending => "Sayısal azalan",
        SortMode.Natural => "Doğal sıralama",

        DelimiterOperationKind.ExtractColumn => "Sütun çıkar",
        DelimiterOperationKind.RemoveColumn => "Sütunu kaldır",
        DelimiterOperationKind.ReorderColumns => "Sütunları sırala",
        DelimiterOperationKind.JoinColumns => "Sütunları birleştir",
        DelimiterOperationKind.FilterColumn => "Sütuna göre filtrele",

        SplitMode.LineCount => "Satır sayısına göre",
        SplitMode.ApproximateBytes => "Yaklaşık boyuta göre",
        SplitMode.BeforeRegex => "Regex eşleşmesinden önce",
        SplitMode.AfterRegex => "Regex eşleşmesinden sonra",

        CompareMode.OnlyInA => "Yalnız A",
        CompareMode.OnlyInB => "Yalnız B",
        CompareMode.InBoth => "Ortak satırlar",
        CompareMode.Different => "Farklı satırlar",

        PipelineStepType.Clean => "Temizle",
        PipelineStepType.Filter => "Filtrele",
        PipelineStepType.Dedupe => "Tekrarları kaldır",
        PipelineStepType.Extract => "Ayıkla",
        PipelineStepType.Sort => "Sırala",
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
