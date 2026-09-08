using System;
using System.Collections.Generic;
using REmind.Charting;
using UnityEngine.UIElements;

/// <summary>Registered choices and the open document's in-memory JSON, never runtime objects.</summary>
internal sealed class ChartEffectNoteEditorPanel
{
    private readonly EffectRegistry registry = EffectRegistry.CreateDefault();
    private readonly TextField musicId = Field(new TextField("Music ID"));
    private readonly TextField difficultyId = Field(new TextField("Difficulty"));
    private readonly DropdownField gimmick = Field(new DropdownField("Gimmick"));
    private readonly DropdownField effect = Field(new DropdownField("Effect"));
    private readonly DropdownField command = Field(new DropdownField("Command"));
    private readonly IntegerField order = Field(new IntegerField("Order"));
    private readonly TextField parameters = Field(new TextField("JSON") { multiline = true });
    private readonly Label identity = new Label();
    private readonly List<string> effectIds = new List<string>();
    private readonly List<string> gimmickIds = new List<string>();
    private readonly List<string> commandIds = new List<string>();
    private readonly Action<string, bool> report;
    private readonly Action copy;
    private readonly Action reload;
    private bool populating;

    public VisualElement Element { get; } = new VisualElement();
    public string MusicId => musicId.value?.Trim();
    public string DifficultyId => difficultyId.value?.Trim();
    public string GimmickId => SelectedId(gimmick, gimmickIds);
    public string EffectTypeId => SelectedId(effect, effectIds);
    public string CommandId => SelectedId(command, commandIds);
    public int Order => order.value;
    public string ParametersJson => parameters.value;

    public ChartEffectNoteEditorPanel(
        Action<string, bool> report,
        Action copy,
        Action reload)
    {
        this.report = report;
        this.copy = copy;
        this.reload = reload;
        musicId.name = "effect-music-id-field";
        difficultyId.name = "effect-difficulty-id-field";
        gimmick.name = "effect-gimmick-field";
        effect.name = "effect-type-field";
        command.name = "effect-command-field";
        order.name = "effect-order-field";
        parameters.name = "effect-parameters-json-field";
        identity.name = "effect-identity-label";
        Element.name = "effect-note-edit-panel";
        Element.AddToClassList("note-type-edit-panel");
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 500;
        Element.Add(scroll);
        var title = new Label("Effect / Song configuration");
        title.AddToClassList("note-edit-section-title");
        scroll.Add(title);
        scroll.Add(musicId);
        scroll.Add(difficultyId);
        scroll.Add(gimmick);
        scroll.Add(effect);
        scroll.Add(command);
        scroll.Add(order);
        identity.AddToClassList("note-edit-meta");
        identity.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(identity);
        parameters.style.height = 125;
        parameters.style.minHeight = 125;
        scroll.Add(parameters);
        AddButton(scroll, "effect-reset-json-button",
            "Reset JSON to selected defaults", ResetParameters);
        AddButton(scroll, "effect-validate-json-button",
            "Validate JSON", ValidateParameters);
        AddButton(scroll, "effect-copy-button",
            "Copy applied Effect to Measure / Pos", () => this.copy());
        AddButton(scroll, "effect-reload-json-button",
            "Reload saved JSON (discard parameter edits)", () => this.reload());
        var note = new Label(
            "Apply commits all fields as one Undo step. Music / difficulty / gimmick are chart-wide. " +
            "Copy uses the last applied settings. JSON is saved beside .rd as " +
            "effect.{musicId}.{difficultyId}.json. Legacy Effects require an explicit type.");
        note.AddToClassList("note-edit-meta");
        note.style.whiteSpace = WhiteSpace.Normal;
        scroll.Add(note);

        effect.RegisterValueChangedCallback(_ =>
        {
            if (populating) return;
            RefreshCommands(string.Empty);
            ResetParameters();
        });
        gimmick.RegisterValueChangedCallback(_ =>
        {
            if (populating) return;
            RefreshCommands(string.Empty);
            if (EffectTypeId == "music.call") ResetParameters();
        });
        command.RegisterValueChangedCallback(_ =>
        {
            if (!populating) ResetParameters();
        });
    }

    public void Populate(ChartHolder holder)
    {
        populating = true;
        try
        {
            musicId.SetValueWithoutNotify(ChartEffectDocumentState.MusicId);
            difficultyId.SetValueWithoutNotify(ChartEffectDocumentState.DifficultyId);
            var effectNames = new List<string>();
            effectIds.Clear();
            effectIds.Add(string.Empty);
            effectNames.Add("Unresolved — select a type");
            foreach (EffectRegistration registration in registry.Effects)
            {
                effectIds.Add(registration.TypeId);
                effectNames.Add($"{registration.DisplayName} [{registration.TypeId}]");
            }
            BindChoices(effect, effectIds, effectNames, holder.effectTypeId);

            var gimmickNames = new List<string> { "None" };
            gimmickIds.Clear();
            gimmickIds.Add(string.Empty);
            foreach (MusicGimmickRegistration registration in registry.Gimmicks)
            {
                gimmickIds.Add(registration.GimmickId);
                gimmickNames.Add($"{registration.DisplayName} [{registration.GimmickId}]");
            }
            BindChoices(gimmick, gimmickIds, gimmickNames, ChartEffectDocumentState.GimmickId);
            RefreshCommands(holder.effectCommandId);
            order.SetValueWithoutNotify(holder.effectOrder);
            parameters.SetValueWithoutNotify(holder.effectParametersJson ?? string.Empty);
            identity.text = "effectId: " + (holder.effectId ?? "assigned when applied");
        }
        finally
        {
            populating = false;
        }
    }

    private void RefreshCommands(string selectedCommandId)
    {
        bool callsGimmick = EffectTypeId == "music.call";
        command.SetEnabled(callsGimmick);
        var names = new List<string> { callsGimmick ? "Select a command" : "Not required" };
        commandIds.Clear();
        commandIds.Add(string.Empty);
        if (callsGimmick && registry.TryGetGimmick(GimmickId,
                out MusicGimmickRegistration registration))
        {
            foreach (MusicGimmickCommandRegistration item in registration.Commands)
            {
                commandIds.Add(item.CommandId);
                names.Add($"{item.DisplayName} [{item.CommandId}]");
            }
        }
        BindChoices(command, commandIds, names,
            callsGimmick ? selectedCommandId : string.Empty);
    }

    private void ResetParameters()
    {
        try
        {
            parameters.SetValueWithoutNotify(
                ChartEffectJsonCodec.GetDefaultJson(EffectTypeId, CommandId, GimmickId)
                ?? string.Empty);
            report(null, false);
        }
        catch (Exception exception)
        {
            parameters.SetValueWithoutNotify(string.Empty);
            report(exception.Message, true);
        }
    }

    private void ValidateParameters()
    {
        bool valid = ChartEffectJsonCodec.TryDecode(
            EffectTypeId, CommandId, GimmickId, ParametersJson, registry,
            out _, out string error);
        report(valid ? "JSON is valid. Apply to keep these settings." : error,
            !valid);
    }

    private static void BindChoices(DropdownField field, List<string> ids,
        List<string> names, string selected)
    {
        selected ??= string.Empty;
        int index = ids.IndexOf(selected);
        if (index < 0)
        {
            index = ids.Count;
            ids.Add(selected);
            names.Add($"Unregistered [{selected}]");
        }
        field.choices = names;
        field.SetValueWithoutNotify(names[index]);
    }

    private static string SelectedId(DropdownField field, List<string> ids)
    {
        int index = field.choices.IndexOf(field.value);
        return index >= 0 && index < ids.Count ? ids[index] : string.Empty;
    }

    private static T Field<T>(T field) where T : VisualElement
    {
        field.AddToClassList("note-edit-field");
        return field;
    }

    private static void AddButton(
        VisualElement parent,
        string name,
        string text,
        Action action)
    {
        var button = new Button(action) { name = name, text = text };
        button.style.minHeight = 26;
        parent.Add(button);
    }
}
