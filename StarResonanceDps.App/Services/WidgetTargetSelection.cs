using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Services;

/// <summary>
/// メイン画面のカードで、複数開けるウィジェット(<see cref="WidgetConfigDefaults.SupportsOpenTargets"/>)の窓の相手を選んで開く・閉じる。
///
/// <para>
/// 項目は名前の選択リストを開いたときにだけ作る(閉じている間は作り直さない)。並べるものは種別で決まる:
/// 人の窓は名簿の全員(自分 → パーティの番号順 → そのほかの名前順)、バフリスト・デバフリストはそれに続けてエンティティリストの敵、
/// 与ダメ・ヒールの詳細とグラフはメーターの行、バフ・デバフカードはカードの設定のショートカットのオンの行(自分のバフ・デバフ、設定の並び)と、
/// バフリスト(デバフはデバフリスト)のカードで選んでいる相手のバフ(デバフ)。
/// 開いている窓の相手と選んでいる項目は、名簿などから外れていても末尾に足す(選んで閉じられるように)。
/// 行の文字は、その項目で開く窓のタイトル(<see cref="WidgetHeaderText"/>)。
/// </para>
///
/// <para>
/// カードの名前の欄も、選んでいる項目で開く窓のタイトルと同じ文字。起動ボタンは、選んでいる項目の窓が開いているかを映す。
/// 並べるもの(と開いている窓)が無いときは、カードは名前だけの表示に戻り、選択リストも開かない。起動ボタンは既定を開く。
/// </para>
///
/// <para>
/// バフ・デバフカードの既定はショートカットの1つ目で、オンの行が1つも無ければ「指定なし」(<see cref="WidgetTargetOption.CreateUnspecifiedBuffCard"/>)。
/// 指定なしの欄の文字はウィジェット名だけで、起動ボタンはいつも起動の絵(押すと、自分のバフリストの1行目のカードか、最初に付いたものを待つ窓を開くか前に出す。
/// <see cref="WidgetWindowManager.OpenUnspecifiedBuffCard"/>)。開くカードが決まったら、選択をそのカードに置き換える(<see cref="ResolveUnspecified"/>、
/// 指定なしは別の項目として残さない)。指定なしは並べるものが1つも無いときだけの項目で、並べるものが出たらその1つ目で上書きし(<see cref="Refresh"/>)、
/// 並べるものが無くなれば既定として戻る。
/// </para>
/// </summary>
public sealed class WidgetTargetSelection
{
    private readonly WidgetWindowManager _windowManager;
    private readonly Func<WidgetKind, WidgetListItemViewModel?> _findWidget;

    public WidgetTargetSelection(
        WidgetWindowManager windowManager,
        Func<WidgetKind, WidgetListItemViewModel?> findWidget)
    {
        _windowManager = windowManager;
        _findWidget = findWidget;
    }

    /// <summary>選んでいる項目の既定。バフ・デバフカードはショートカットの1つ目(無ければ「指定なし」)、ほかは自分。</summary>
    public static WidgetTargetOption CreateDefault(WidgetListItemViewModel widget)
    {
        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            return WidgetTargetOption.CreateSelf(AppState.PlayerUID);
        }

        return CreateShortcutOptions(widget).FirstOrDefault()
            ?? WidgetTargetOption.CreateUnspecifiedBuffCard();
    }

    /// <summary>
    /// 名前の選択リストの項目を作り直す。並べるものが1つも無ければ(前の当て直しの後に消えた)、カードを当て直して名前だけの表示に戻す
    /// (開いたリストは画面のコードが閉じる)。
    /// </summary>
    public void BuildOptions(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        var options = CreateListedOptions(widget);
        if (options.Count > 0)
        {
            AppendSelectedTarget(widget, options);
        }

        widget.TargetOptions = [.. options.Select(option => CreateRow(widget, option))];
        if (options.Count == 0)
        {
            Refresh(widget);
        }
    }

    /// <summary>リストの行。文字はその項目で開く窓のタイトル(<see cref="FormatSelectorText"/>)、円の色はその窓の状態の印(開いていなければ null)。</summary>
    private WidgetTargetOption CreateRow(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        var (person, buff) = Resolve(widget, option);
        return option.WithPresentation(
            FormatSelectorText(widget, option),
            _windowManager.FindTargetWindow(widget, person, buff)?.DataStatus.Level);
    }

    /// <summary>並べるもの(候補と、候補に無い開いている窓の相手)。選んでいる項目は足さない。</summary>
    private List<WidgetTargetOption> CreateListedOptions(WidgetListItemViewModel widget)
    {
        var options = CreateCandidates(widget);
        AppendOpenTargets(widget, options);
        return options;
    }

    /// <summary>選んでいる項目の窓を開くか閉じる(起動ボタン)。バフ・デバフカードの「指定なし」は開くか前に出すだけ。</summary>
    public void Toggle(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        var selected = GetSelected(widget);
        if (selected.IsUnspecifiedBuffCard)
        {
            _windowManager.OpenUnspecifiedBuffCard(
                widget,
                ResolvePlayerIdentity(WidgetTargetOption.CreateSelf(AppState.PlayerUID)).UserId);
            Refresh(widget);
            return;
        }

        var (person, buff) = Resolve(widget, selected);
        if (_windowManager.FindTargetWindow(widget, person, buff) is not null)
        {
            _windowManager.CloseTarget(widget, person, buff);
        }
        else
        {
            _windowManager.OpenTarget(widget, person, buff);
        }

        Refresh(widget);
    }

    /// <summary>
    /// カードの名前の欄と起動ボタンを、選んでいる項目と開いている窓で当て直す。値が同じなら変えない。
    /// 並べるものが無いときは名前だけの表示(直す前と同じ)になるので、起動ボタンも直す前と同じく既定を開くよう、選択を既定に戻す。
    /// バフ・デバフカードの「指定なし」は並べるものが1つも無いときだけの項目で、並べるものが出たらその1つ目で上書きする。
    /// 選択リストの元のうちバフの行とメーターの行には変わった合図が無いので、メイン画面が全部のカードについて1秒ごとにここを呼ぶ。
    /// </summary>
    public void Refresh(WidgetListItemViewModel widget)
    {
        if (!widget.SupportsTargetSelection)
        {
            return;
        }

        // バフ・デバフカードは並べるもの(ショートカット・リストのカードの相手の行・開いている窓)を作って数える。指定なしの上書きにも使う。
        var listed = widget.Kind == WidgetKind.BuffDebuffCard ? CreateListedOptions(widget) : null;
        var hasOptions = listed is not null ? listed.Count > 0 : HasOptions(widget);
        widget.HasTargetOptions = hasOptions;
        var defaultOption = CreateDefault(widget);
        if (!hasOptions && widget.SelectedTargetOption is { } current && !current.IsSameTarget(defaultOption))
        {
            widget.SelectedTargetOption = defaultOption;
        }
        else if (listed is { Count: > 0 } && widget.SelectedTargetOption?.IsUnspecifiedBuffCard == true)
        {
            widget.SelectedTargetOption = listed[0];
        }

        var selected = GetSelected(widget);
        widget.TargetSelectorText = FormatSelectorText(widget, selected);

        // 「指定なし」は開くか前に出すだけなので、いつも起動の絵。
        var (person, buff) = Resolve(widget, selected);
        widget.IsSelectedTargetOpen = !selected.IsUnspecifiedBuffCard
            && _windowManager.FindTargetWindow(widget, person, buff) is not null;
    }

    /// <summary>
    /// 「指定なし」で開くカードが決まった(<see cref="WidgetWindowManager.UnspecifiedBuffCardResolved"/>)。今の選択が指定なしなら、
    /// そのカードの項目に置き換える(欄はその窓のタイトル、起動ボタンはその窓を映す)。ほかの項目を選んでいれば置き換えない。
    /// </summary>
    public void ResolveUnspecified(WidgetListItemViewModel widget, WidgetTargetOption card)
    {
        if (widget.SelectedTargetOption?.IsUnspecifiedBuffCard == true)
        {
            widget.SelectedTargetOption = card;
        }
    }

    public void RefreshAll(IEnumerable<WidgetListItemViewModel> widgets)
    {
        foreach (var widget in widgets)
        {
            Refresh(widget);
        }
    }

    private static WidgetTargetOption GetSelected(WidgetListItemViewModel widget)
    {
        return widget.SelectedTargetOption ??= CreateDefault(widget);
    }

    /// <summary>
    /// 並べるもの(と開いている窓)があるか。リストを作らずに、件数や1人でもいるかだけを見る。
    /// バフ・デバフカードはここを使わない(<see cref="Refresh"/> が並べるものを作って数える)。
    /// </summary>
    private bool HasOptions(WidgetListItemViewModel widget)
    {
        if (GetOpenTargets(widget).Count > 0)
        {
            return true;
        }

        return widget.Kind switch
        {
            WidgetKind.DamageContribution or WidgetKind.DamageSummary or WidgetKind.DpsGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Damage, GetPartyDisplayMode(WidgetKind.DpsMeter)),
            WidgetKind.HealingContribution or WidgetKind.HealingSummary or WidgetKind.HpsGraph
                => MeterSnapshotProvider.HasPlayers(MeterSnapshotKind.Healing, GetPartyDisplayMode(WidgetKind.HpsMeter)),
            WidgetKind.DamageTakenSummary => MeterSnapshotProvider.HasDamageTakenPlayers(),
            _ => PlayerRosterPresentationStore.Instance.Current.Entries.Count > 0
                || MeterSnapshotProvider.GetSelfPlayerIdentity() is not null
                || (widget.Kind is WidgetKind.BuffList or WidgetKind.DebuffList
                    && _findWidget(WidgetKind.EntityList)?.EntityListEntries.Count > 0)
        };
    }

    private List<WidgetTargetOption> CreateCandidates(WidgetListItemViewModel widget)
    {
        return widget.Kind switch
        {
            WidgetKind.BuffDebuffCard => CreateBuffCardOptions(widget),
            WidgetKind.DamageContribution or WidgetKind.DamageSummary or WidgetKind.DpsGraph
                => CreateMeterOptions(MeterSnapshotKind.Damage, WidgetKind.DpsMeter),
            WidgetKind.HealingContribution or WidgetKind.HealingSummary or WidgetKind.HpsGraph
                => CreateMeterOptions(MeterSnapshotKind.Healing, WidgetKind.HpsMeter),
            WidgetKind.DamageTakenSummary => CreateDamageTakenOptions(),
            _ => CreateRosterOptions(includesEntities: widget.Kind is WidgetKind.BuffList or WidgetKind.DebuffList)
        };
    }

    /// <summary>
    /// 項目から、開く窓の相手とバフを決める。バフ・デバフカードの相手は、料理・薬剤のまとまりなら自分、
    /// それ以外はバフリスト(デバフはデバフリスト)のカードで今選んでいる項目。
    /// </summary>
    private (WidgetTargetOption Person, WidgetTargetOption? Buff) Resolve(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        if (widget.Kind != WidgetKind.BuffDebuffCard)
        {
            return (option, null);
        }

        return (ResolveBuffPerson(option), option);
    }

    private WidgetTargetOption ResolveBuffPerson(WidgetTargetOption buff)
    {
        if (buff.IsSelf)
        {
            return WidgetTargetOption.CreateSelf(AppState.PlayerUID);
        }

        var listWidget = _findWidget(buff.BuffListKind == PlayerBuffListKind.Debuff ? WidgetKind.DebuffList : WidgetKind.BuffList);
        return listWidget?.SelectedTargetOption ?? WidgetTargetOption.CreateSelf(AppState.PlayerUID);
    }

    /// <summary>
    /// その項目で開く窓のタイトルと同じ文字。カードの名前の欄とリストの行に使う。
    /// 窓が開いていれば窓のタイトルそのもの(再起動の直後は窓が保存しておいた名前で名乗り、名簿にはまだ相手がいない)。
    /// </summary>
    private string FormatSelectorText(WidgetListItemViewModel widget, WidgetTargetOption option)
    {
        // 「指定なし」で開く窓は決まっていない(1行目のカードか、待つ窓)。待つ窓のタイトルと同じくウィジェット名だけ。
        if (option.IsUnspecifiedBuffCard)
        {
            return widget.DisplayName;
        }

        var (person, buff) = Resolve(widget, option);
        if (_windowManager.FindTargetWindow(widget, person, buff) is { } window)
        {
            return window.HeaderText;
        }

        if (option.Kind == WidgetTargetOptionKind.Buff)
        {
            var prefix = BuffDebuffCardContent.CreateHeaderPrefix(
                widget.DisplayName,
                option.BuffListKind,
                option.BuffGroup,
                option.BuffName);
            return FormatPersonText(prefix, ResolveBuffPerson(option));
        }

        return FormatPersonText(widget.DisplayName, option);
    }

    private static string FormatPersonText(string prefix, WidgetTargetOption person)
    {
        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            return WidgetHeaderText.Join(prefix, person.Entity?.DisplayName);
        }

        var identity = ResolvePlayerIdentity(person);
        return WidgetHeaderText.ForPlayer(
            prefix,
            identity.Name,
            identity.UserId,
            identity.IsSelf,
            identity.IsNpc,
            identity.ProfessionId,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    /// <summary>
    /// 人の素性。窓と同じく、名簿の行、無ければメーターの実体から引く。どちらにも無ければ、自分は何も分からない、
    /// ほかの人は相手の指定の UID だけ(窓のタイトルも相手の指定の UID を出す)。
    /// </summary>
    private static PlayerIdentity ResolvePlayerIdentity(WidgetTargetOption person)
    {
        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        if (person.IsSelf)
        {
            if (roster.FirstOrDefault(entry => entry.IsSelf) is { } selfRow)
            {
                return PlayerIdentity.FromRoster(selfRow);
            }

            return MeterSnapshotProvider.GetSelfPlayerIdentity() is { } selfIdentity
                ? PlayerIdentity.FromMeter(selfIdentity, isSelf: true)
                : new PlayerIdentity(null, 0, true, false, 0);
        }

        if (roster.FirstOrDefault(entry => entry.CharacterId == person.CharacterId) is { } row)
        {
            return PlayerIdentity.FromRoster(row);
        }

        var isSelf = AppState.PlayerUID != 0 && person.CharacterId == AppState.PlayerUID;
        return MeterSnapshotProvider.GetPlayerIdentity(person.CharacterId) is { } identity
            ? PlayerIdentity.FromMeter(identity, isSelf)
            : new PlayerIdentity(null, person.CharacterId, isSelf, false, 0);
    }

    /// <summary>名簿の全員(自分 → パーティの番号順 → そのほかの名前順)。バフリスト・デバフリストはエンティティリストの敵を続ける。</summary>
    private List<WidgetTargetOption> CreateRosterOptions(bool includesEntities)
    {
        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var options = new List<WidgetTargetOption>();

        if (roster.FirstOrDefault(entry => entry.IsSelf) is { } selfRow)
        {
            options.Add(CreatePlayerOption(PlayerIdentity.FromRoster(selfRow)));
        }
        else if (MeterSnapshotProvider.GetSelfPlayerIdentity() is { } selfIdentity)
        {
            options.Add(CreatePlayerOption(PlayerIdentity.FromMeter(selfIdentity, isSelf: true)));
        }

        options.AddRange(roster
            .Where(entry => !entry.IsSelf && entry.IsPartyMember)
            .OrderBy(entry => entry.PartyNumber ?? int.MaxValue)
            .Select(entry => CreatePlayerOption(PlayerIdentity.FromRoster(entry))));

        options.AddRange(roster
            .Where(entry => !entry.IsSelf && !entry.IsPartyMember)
            .Select(entry => PlayerIdentity.FromRoster(entry))
            .OrderBy(identity => identity.DisplayName, StringComparer.CurrentCulture)
            .Select(CreatePlayerOption));

        if (includesEntities && _findWidget(WidgetKind.EntityList) is { } entityList)
        {
            options.AddRange(entityList.EntityListEntries.Select(CreateEntityOption));
        }

        return options;
    }

    /// <summary>メーターに出ている行(メーターの並びと、メーターのカードの設定のパーティ表示のまま)。</summary>
    private List<WidgetTargetOption> CreateMeterOptions(MeterSnapshotKind kind, WidgetKind meterKind)
    {
        var snapshot = MeterSnapshotProvider.GetSnapshot(kind, GetPartyDisplayMode(meterKind));
        return [.. snapshot.Players.Select(player => new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Player,
            IsSelf = player.IsSelf,
            // メーターの行から開くときと同じ相手の指定(MeterPlayerEntry.PlayerId)。
            CharacterId = player.UserId != 0 ? player.UserId : player.CharacterId
        })];
    }

    /// <summary>被ダメ詳細。被ダメログに出る人(その回で被弾の記録がある人)を、被ダメの合計の多い順。</summary>
    private static List<WidgetTargetOption> CreateDamageTakenOptions()
    {
        return [.. MeterSnapshotProvider.GetDamageTakenPlayers().Select(player => new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Player,
            IsSelf = player.IsSelf,
            // 被ダメログの行から開くときと同じ相手の指定(DamageTakenListEntry.CharacterId)。
            CharacterId = player.CharacterId
        })];
    }

    private PartyDisplayMode GetPartyDisplayMode(WidgetKind meterKind)
    {
        return (PartyDisplayMode)(_findWidget(meterKind)?.GetMeterSettingsSnapshot().PartyDisplayModeIndex ?? 0);
    }

    /// <summary>
    /// 先頭にショートカットのオンの行(<see cref="CreateShortcutOptions"/>)、
    /// 続けてバフリストのカードで選んでいる相手のバフ、デバフリストのカードで選んでいる相手のデバフ。
    /// </summary>
    private List<WidgetTargetOption> CreateBuffCardOptions(WidgetListItemViewModel widget)
    {
        var options = CreateShortcutOptions(widget);
        AppendBuffRows(options, PlayerBuffListKind.Buff, WidgetKind.BuffList);
        AppendBuffRows(options, PlayerBuffListKind.Debuff, WidgetKind.DebuffList);
        return options;
    }

    /// <summary>
    /// バフ・デバフカードの設定のショートカットのオンの行(設定の並び)から、自分のカードの項目を作る(相手はいつも自分)。
    /// バフかデバフか分からない番号は作らない(<see cref="CreateSelfGroupEntryOption"/>)。
    /// </summary>
    private static List<WidgetTargetOption> CreateShortcutOptions(WidgetListItemViewModel widget)
    {
        var options = new List<WidgetTargetOption>();
        foreach (var key in widget.GetEnabledBuffCardShortcuts())
        {
            if (CreateSelfGroupEntryOption(key) is { } option)
            {
                options.Add(option);
            }
        }

        return options;
    }

    /// <summary>
    /// 行の鍵(ショートカット・バフ・デバフグループの行)から、自分のカードの項目を作る。開き方はグループの行の「バフカード」「デバフカード」と同じ
    /// (<see cref="WidgetWindowManager.OpenBuffCardForGroupEntry"/>)。バフかデバフか分からない番号は作らない。
    /// </summary>
    private static WidgetTargetOption? CreateSelfGroupEntryOption(BuffEntryKey key)
    {
        if (key.IsGroup)
        {
            return WidgetTargetOption.CreateSelfBuffGroup(key.Group);
        }

        if (CombatDataCatalog.GetBuffListKind(key.BuffId) is not { } kind)
        {
            return null;
        }

        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Buff,
            IsSelf = true,
            BuffListKind = kind,
            BuffKey = MeterSnapshotProvider.CreateBuffCardKey(key.BuffId),
            BuffName = CombatDataCatalog.GetBuffDisplayNameWithoutInternalId(key.BuffId, SourceLanding.None)
        };
    }

    /// <summary>同じカード(同じ相手の、同じ種類・まとまり・鍵)を指す項目か。</summary>
    private bool IsSameCard(WidgetTargetOption existing, WidgetTargetOption buff, WidgetTargetOption person)
    {
        return existing.Kind == WidgetTargetOptionKind.Buff
            && existing.BuffListKind == buff.BuffListKind
            && existing.BuffGroup == buff.BuffGroup
            && string.Equals(existing.BuffKey, buff.BuffKey, StringComparison.Ordinal)
            && IsSamePerson(ResolveBuffPerson(existing), person);
    }

    /// <summary>
    /// リストのカードで選んでいる相手のバフ(デバフ)を、そのリストと同じ作り方(そのカードの設定の非表示リスト・同種バフの表示)で並べる。
    /// 1枚のカードはバフの番号ごと(<c>PlayerBuffSnapshot.CardKey</c>)なので、行はカードに出す代表の行だけ。
    /// 先頭のショートカットに同じカード(<see cref="IsSameCard"/>、相手が自分のとき)があれば、そちらに任せて並べない。
    /// </summary>
    private void AppendBuffRows(List<WidgetTargetOption> options, PlayerBuffListKind kind, WidgetKind listKind)
    {
        var listWidget = _findWidget(listKind);
        var person = listWidget?.SelectedTargetOption ?? WidgetTargetOption.CreateSelf(AppState.PlayerUID);
        var settings = listWidget?.GetBuffListSettingsSnapshot() ?? WidgetConfigDefaults.CreateBuffListSettings(listKind);

        IReadOnlyList<PlayerBuffSnapshot> rows;
        if (person.Kind == WidgetTargetOptionKind.Entity)
        {
            if (person.Entity is not { } entity)
            {
                return;
            }

            rows = BuffListSnapshotFilter.Apply(
                settings,
                MeterSnapshotProvider.GetEntityBuffs(
                    entity.EntityUuid,
                    kind,
                    WidgetConfigDefaults.GroupsSameBuffs(settings, isEntityTarget: true)));
        }
        else
        {
            var identity = ResolvePlayerIdentity(person);
            if (identity.UserId == 0)
            {
                return;
            }

            rows = BuffListSnapshotFilter.GetPlayerRows(settings, identity.UserId, kind);
        }

        foreach (var snapshot in rows)
        {
            if (!snapshot.IsCardRepresentative)
            {
                continue;
            }

            // バフリストの行から開くのと同じく、行のバフのまとまりで開く(料理・薬剤はまとまりで、バフのキーを持たない。ショートカットと同じ形)。
            var group = CombatDataCatalog.GetBuffGroup(snapshot.BaseId);
            var row = new WidgetTargetOption
            {
                Kind = WidgetTargetOptionKind.Buff,
                BuffListKind = kind,
                BuffKey = group == BuffGroup.None ? snapshot.CardKey : null,
                BuffGroup = group,
                BuffName = snapshot.NameWithoutInternalId
            };

            if (!options.Any(existing => IsSameCard(existing, row, person)))
            {
                options.Add(row);
            }
        }
    }

    /// <summary>
    /// 開いている窓の相手。バフ・デバフカードの待っている窓(「指定なし」)は除く(指定なしは並べるものが無いときの形で、起動ボタンでは閉じないため)。
    /// </summary>
    private IReadOnlyList<(WidgetTargetOption Person, WidgetTargetOption? Buff)> GetOpenTargets(WidgetListItemViewModel widget)
    {
        return [.. _windowManager.GetOpenTargets(widget).Where(target => target.Buff?.IsUnspecifiedBuffCard != true)];
    }

    /// <summary>開いている窓の相手で、リストに無いものを末尾に足す。バフ・デバフカードは、今のリストの相手のカードだけ(相手を項目で指せないため)。</summary>
    private void AppendOpenTargets(WidgetListItemViewModel widget, List<WidgetTargetOption> options)
    {
        foreach (var (person, buff) in GetOpenTargets(widget))
        {
            if (widget.Kind == WidgetKind.BuffDebuffCard)
            {
                if (buff is null
                    || options.Any(existing => IsSameCard(existing, buff, person))
                    || (!buff.IsSelf && !IsSamePerson(ResolveBuffPerson(buff), person)))
                {
                    continue;
                }

                options.Add(buff);
                continue;
            }

            if (!options.Any(existing => existing.IsSameTarget(person)))
            {
                options.Add(person);
            }
        }
    }

    /// <summary>
    /// 選んでいる項目がリストに無ければ末尾に足す。バフ・デバフカードは同じカード(<see cref="IsSameCard"/>)があれば足さない
    /// (「指定なし」から置き換えた自分のカードと、バフリストのカードで自分を選んでいるときの自分のバフの行は、同じカードを指す)。
    /// </summary>
    private void AppendSelectedTarget(WidgetListItemViewModel widget, List<WidgetTargetOption> options)
    {
        var selected = GetSelected(widget);
        var isListed = widget.Kind == WidgetKind.BuffDebuffCard && !selected.IsUnspecifiedBuffCard
            ? options.Any(option => IsSameCard(option, selected, ResolveBuffPerson(selected)))
            : options.Any(option => option.IsSameTarget(selected));
        if (!isListed)
        {
            options.Add(selected);
        }
    }

    private static bool IsSamePerson(WidgetTargetOption left, WidgetTargetOption right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        if (left.Kind == WidgetTargetOptionKind.Entity)
        {
            return left.Entity?.EntityUuid == right.Entity?.EntityUuid;
        }

        var leftId = left.IsSelf ? AppState.PlayerUID : left.CharacterId;
        var rightId = right.IsSelf ? AppState.PlayerUID : right.CharacterId;
        return (left.IsSelf && right.IsSelf) || (leftId != 0 && leftId == rightId);
    }

    private static WidgetTargetOption CreatePlayerOption(PlayerIdentity identity)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Player,
            IsSelf = identity.IsSelf,
            CharacterId = identity.UserId
        };
    }

    private static WidgetTargetOption CreateEntityOption(EntityListEntry entity)
    {
        return new WidgetTargetOption
        {
            Kind = WidgetTargetOptionKind.Entity,
            Entity = entity
        };
    }

    /// <summary>人の素性。名前は伏せ字なら null(伏せるかは名前の規則が決める)。</summary>
    private sealed record PlayerIdentity(
        string? Name,
        long UserId,
        bool IsSelf,
        bool IsNpc,
        int ProfessionId)
    {
        /// <summary>名前の規則を通した名前(名前が無ければ UID)。並べ替えに使う。</summary>
        public string DisplayName => PlayerInfoFormatFormatter.GetDisplayName(
            Name,
            UserId,
            IsSelf,
            IsNpc,
            ProfessionId,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        public static PlayerIdentity FromRoster(PlayerRosterEntry row)
        {
            return new PlayerIdentity(
                NameOrNull(row.Name),
                row.CharacterId,
                row.IsSelf,
                row.IsNpc,
                row.ProfessionId);
        }

        public static PlayerIdentity FromMeter(MeterPlayerIdentity identity, bool isSelf)
        {
            return new PlayerIdentity(
                NameOrNull(identity.Name),
                identity.UserId,
                isSelf,
                identity.IsNpc,
                identity.ProfessionId);
        }

        private static string? NameOrNull(string? name)
        {
            return string.Equals(name, PlayerRosterPresentationStore.HiddenPlayerName, StringComparison.Ordinal)
                ? null
                : name;
        }
    }
}
