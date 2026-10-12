using System;

namespace StarResonanceDps.App.Views;

/// <summary>メッセージの窓の詳細に出す例外の文。一番内側の例外の型と文言(全文はログの側)。</summary>
public static class ExceptionDetailFormatter
{
    public static string Format(Exception exception)
    {
        var cause = exception.GetBaseException();
        return $"{cause.GetType().Name}: {cause.Message}";
    }
}
