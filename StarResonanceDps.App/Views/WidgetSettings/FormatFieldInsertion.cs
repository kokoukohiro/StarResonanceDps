using System.Windows.Controls;

namespace StarResonanceDps.App.Views.WidgetSettings;

/// <summary>
/// 書式のカスタマイズ欄へ項目を差し込む共通処理。
///
/// <para>
/// カーソル位置はViewModelから見えないので、挿入はビュー側でやる。
/// プレイヤー名・エンティティ名・バフ名の3か所が同じ振る舞いをする。
/// </para>
/// </summary>
internal static class FormatFieldInsertion
{
    /// <summary>カーソル位置へ差し込む。文字を選んでいればそれを置き換える。</summary>
    public static void Insert(TextBox textBox, string? placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            return;
        }

        var insertAt = textBox.SelectionStart;
        textBox.SelectedText = placeholder;
        textBox.CaretIndex = insertAt + placeholder.Length;
        textBox.Focus();
    }

    /// <summary>
    /// 入力欄に触れていない間はカーソルを末尾に置く。
    ///
    /// <para>
    /// WPFの初期カーソル位置は0なので、そのままだと<b>一度もクリックしていない欄で先頭に入る</b>。
    /// 従来の「末尾に追加」と食い違うので、フォーカスが無いうちは末尾へ寄せておく。
    /// 設定の読み込み直しやリセットで中身が入れ替わったときも同じ。
    /// </para>
    /// </summary>
    public static void MoveCaretToEndWhenUnfocused(TextBox textBox)
    {
        if (!textBox.IsKeyboardFocusWithin)
        {
            textBox.CaretIndex = textBox.Text.Length;
        }
    }
}
