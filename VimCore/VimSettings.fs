﻿#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open System.ComponentModel.Composition
open Vim.GlobalSettingNames
open Vim.LocalSettingNames
open Vim.WindowSettingNames

// TODO: We need to add verification for setting options which can contain
// a finite list of values.  For example backspace, virtualedit, etc ...  Setting
// them to an invalid value should produce an error

type internal SettingsMap
    (
        _rawData : (string * string * SettingValue) seq,
        _isGlobal : bool
    ) =

    let _settingChangedEvent = StandardEvent<SettingEventArgs>()

    /// Create the settings off of the default map
    let mutable _settings =
         _rawData
         |> Seq.map (fun (name, abbrev, value) -> {Name=name; Abbreviation=abbrev; DefaultValue=value; Value=value; IsGlobal=_isGlobal})
         |> Seq.map (fun setting -> (setting.Name,setting))
         |> Map.ofSeq

    member x.AllSettings = _settings |> Map.toSeq |> Seq.map (fun (_,value) -> value)
    member x.OwnsSetting settingName = x.GetSetting settingName |> Option.isSome
    member x.SettingChanged = _settingChangedEvent.Publish

    /// Replace a Setting with a new value
    member x.ReplaceSetting settingName setting = 
        _settings <- _settings |> Map.add settingName setting

        let args = SettingEventArgs(setting)
        _settingChangedEvent.Trigger x args

    member x.TrySetValue settingNameOrAbbrev (value : SettingValue) =

        match x.GetSetting settingNameOrAbbrev with
        | None -> false
        | Some setting ->
            if setting.Kind = value.SettingKind then
                let setting = { setting with Value = value }
                _settings <- _settings |> Map.add setting.Name setting
                _settingChangedEvent.Trigger x (SettingEventArgs(setting))
                true
            else false

    member x.TrySetValueFromString settingNameOrAbbrev strValue = 
        match x.GetSetting settingNameOrAbbrev with
        | None -> false
        | Some setting ->
            match x.ConvertStringToValue strValue setting.Kind with
            | None -> false
            | Some(value) -> x.TrySetValue setting.Name value

    member x.GetSetting settingName : Setting option = 
        match _settings |> Map.tryFind settingName with
        | Some s -> Some s
        | None -> 
            _settings 
            |> Map.toSeq 
            |> Seq.map (fun (_,value) -> value) 
            |> Seq.tryFind (fun setting -> setting.Abbreviation = settingName)

    /// Get a boolean setting value.  Will throw if the setting name does not exist
    member x.GetBoolValue settingName = 
        let setting = _settings |> Map.find settingName
        match setting.Value.AggregateValue with
        | SettingValue.ToggleValue b -> b 
        | SettingValue.NumberValue _ -> failwith "invalid"
        | SettingValue.StringValue _ -> failwith "invalid"
        | SettingValue.CalculatedNumber _ -> failwith "invalid"

    /// Get a string setting value.  Will throw if the setting name does not exist
    member x.GetStringValue settingName =
        let setting = _settings |> Map.find settingName
        match setting.Value.AggregateValue with
        | SettingValue.StringValue s -> s
        | SettingValue.NumberValue _ -> failwith "invalid"
        | SettingValue.ToggleValue _ -> failwith "invalid"
        | SettingValue.CalculatedNumber _ -> failwith "invalid"

    /// Get a number setting value.  Will throw if the setting name does not exist
    member x.GetNumberValue settingName =
        let setting = _settings |> Map.find settingName
        match setting.Value.AggregateValue with
        | SettingValue.NumberValue n -> n
        | SettingValue.StringValue _ -> failwith "invalid"
        | SettingValue.ToggleValue _ -> failwith "invalid"
        | SettingValue.CalculatedNumber _ -> failwith "invalid"

    member x.ConvertStringToValue str kind =
        
        let convertToNumber() = 
            let ret,value = System.Int32.TryParse str
            if ret then Some (SettingValue.NumberValue value) else None
        let convertToBoolean() =
            let ret,value = System.Boolean.TryParse str
            if ret then Some (SettingValue.ToggleValue value) else None
        match kind with
        | SettingKind.NumberKind -> convertToNumber()
        | SettingKind.ToggleKind -> convertToBoolean()
        | SettingKind.StringKind -> Some (SettingValue.StringValue str)

type internal GlobalSettings() =
    static let _disableAllCommand = KeyInputUtil.ApplyModifiersToVimKey VimKey.F12 (KeyModifiers.Control ||| KeyModifiers.Shift)

    static let _globalSettings = 
        [|
            (BackspaceName, "bs", SettingValue.StringValue "")
            (CaretOpacityName, CaretOpacityName, SettingValue.NumberValue 65)
            (ClipboardName, "cb", SettingValue.StringValue "")
            (HighlightSearchName, "hls", SettingValue.ToggleValue false)
            (HistoryName, "hi", SettingValue.NumberValue(Constants.DefaultHistoryLength))
            (IncrementalSearchName, "is", SettingValue.ToggleValue false)
            (IgnoreCaseName,"ic", SettingValue.ToggleValue false)
            (JoinSpacesName, "js", SettingValue.ToggleValue true)
            (KeyModelName, "km", SettingValue.StringValue "")
            (MagicName, MagicName, SettingValue.ToggleValue true)
            (MaxMapCount, MaxMapCount, SettingValue.NumberValue 1000)
            (MaxMapDepth, "mmd", SettingValue.NumberValue 1000)
            (MouseModelName, "mousem", SettingValue.StringValue "popup")
            (ParagraphsName, "para", SettingValue.StringValue "IPLPPPQPP TPHPLIPpLpItpplpipbp")
            (SectionsName, "sect", SettingValue.StringValue "SHNHH HUnhsh")
            (SelectionName, "sel", SettingValue.StringValue "inclusive")
            (SelectModeName, "slm", SettingValue.StringValue "")
            (ScrollOffsetName, "so", SettingValue.NumberValue 0)
            (ShellName, "sh", "ComSpec" |> SystemUtil.GetEnvironmentVariable |> SettingValue.StringValue)
            (ShellFlagName, "shcf", SettingValue.StringValue "/c")
            (SmartCaseName, "scs", SettingValue.ToggleValue false)
            (StartOfLineName, "sol", SettingValue.ToggleValue true)
            (TabStopName, "ts", SettingValue.NumberValue 8)
            (TildeOpName, "top", SettingValue.ToggleValue false)
            (TimeoutName, "to", SettingValue.ToggleValue true)
            (TimeoutExName, TimeoutExName, SettingValue.ToggleValue false)
            (TimeoutLengthName, "tm", SettingValue.NumberValue 1000)
            (TimeoutLengthExName, "ttm", SettingValue.NumberValue -1)
            (UseEditorIndentName, UseEditorIndentName, SettingValue.ToggleValue true)
            (UseEditorSettingsName, UseEditorSettingsName, SettingValue.ToggleValue true)
            (VimRcName, VimRcName, SettingValue.StringValue(StringUtil.empty))
            (VimRcPathsName, VimRcPathsName, SettingValue.StringValue(StringUtil.empty))
            (VirtualEditName, "ve", SettingValue.StringValue(StringUtil.empty))
            (VisualBellName, "vb", SettingValue.ToggleValue false)
            (WrapScanName, "ws", SettingValue.ToggleValue true)
        |]

    let _map = SettingsMap(_globalSettings, true)

    /// Mappings between the setting names and the actual options
    static let ClipboardOptionsMapping = 
        [
            ("unnamed", ClipboardOptions.Unnamed)
            ("autoselect", ClipboardOptions.AutoSelect)
            ("autoselectml", ClipboardOptions.AutoSelectMl)
        ]

    /// Mappings between the setting names and the actual options
    static let SelectModeOptionsMapping = 
        [  
            ("mouse", SelectModeOptions.Mouse)
            ("key", SelectModeOptions.Keyboard)
            ("cmd", SelectModeOptions.Command)
        ]

    /// Mappings between the setting names and the actual options
    static let KeyModelOptionsMapping =
        [
            ("startsel", KeyModelOptions.StartSelection)
            ("stopsel", KeyModelOptions.StopSelection)
        ]

    static member DisableAllCommand = _disableAllCommand

    member x.IsCommaSubOptionPresent optionName suboptionName =
        _map.GetStringValue optionName
        |> StringUtil.split ','
        |> Seq.exists (fun x -> StringUtil.isEqual suboptionName x)

    /// Convert a comma separated option into a set of type safe options
    member x.GetCommaOptions name mappingList emptyOption combineFunc = 
        _map.GetStringValue name 
        |> StringUtil.split ',' 
        |> Seq.fold (fun (options : 'a) (current : string)->
            match List.tryFind (fun (name, _) -> name = current) mappingList with
            | None -> options
            | Some (_, value) -> combineFunc options value) emptyOption

    /// Convert a type safe set of options into a comma separated string
    member x.SetCommaOptions name mappingList options testFunc = 
        let settingValue = 
            mappingList
            |> Seq.ofList
            |> Seq.map (fun (name, value) ->
                if testFunc options value then 
                    Some name
                else 
                    None)
            |> SeqUtil.filterToSome
            |> String.concat ","
        _map.TrySetValue name (SettingValue.StringValue settingValue) |> ignore

    member x.SelectionKind = 
        match _map.GetStringValue SelectionName with
        | "inclusive" -> SelectionKind.Inclusive
        | "old" -> SelectionKind.Exclusive
        | _ -> SelectionKind.Exclusive

    interface IVimGlobalSettings with
        // IVimSettings

        member x.AllSettings = _map.AllSettings
        member x.TrySetValue settingName value = _map.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = _map.TrySetValueFromString settingName strValue
        member x.GetSetting settingName = _map.GetSetting settingName

        // IVimGlobalSettings 
        member x.Backspace 
            with get() = _map.GetStringValue BackspaceName
            and set value = _map.TrySetValue BackspaceName (SettingValue.StringValue value) |> ignore
        member x.CaretOpacity
            with get() = _map.GetNumberValue CaretOpacityName
            and set value = _map.TrySetValue CaretOpacityName (SettingValue.NumberValue value) |> ignore
        member x.Clipboard
            with get() = _map.GetStringValue ClipboardName
            and set value = _map.TrySetValue ClipboardName (SettingValue.StringValue value) |> ignore
        member x.ClipboardOptions
            with get() = x.GetCommaOptions ClipboardName ClipboardOptionsMapping ClipboardOptions.None (fun x y -> x ||| y)
            and set value = x.SetCommaOptions ClipboardName ClipboardOptionsMapping value Util.IsFlagSet
        member x.HighlightSearch
            with get() = _map.GetBoolValue HighlightSearchName
            and set value = _map.TrySetValue HighlightSearchName (SettingValue.ToggleValue value) |> ignore
        member x.History
            with get () = _map.GetNumberValue HistoryName
            and set value = _map.TrySetValue HistoryName (SettingValue.NumberValue value) |> ignore
        member x.IgnoreCase
            with get()  = _map.GetBoolValue IgnoreCaseName
            and set value = _map.TrySetValue IgnoreCaseName (SettingValue.ToggleValue value) |> ignore
        member x.IncrementalSearch
            with get() = _map.GetBoolValue IncrementalSearchName
            and set value = _map.TrySetValue IncrementalSearchName (SettingValue.ToggleValue value) |> ignore
        member x.IsSelectionInclusive = x.SelectionKind = SelectionKind.Inclusive
        member x.IsSelectionPastLine = 
            match _map.GetStringValue SelectionName with
            | "exclusive" -> true
            | "inclusive" -> true
            | _ -> false
        member x.JoinSpaces 
            with get() = _map.GetBoolValue JoinSpacesName
            and set value = _map.TrySetValue JoinSpacesName (SettingValue.ToggleValue value) |> ignore
        member x.KeyModel 
            with get() = _map.GetStringValue KeyModelName
            and set value = _map.TrySetValue KeyModelName (SettingValue.StringValue value) |> ignore
        member x.KeyModelOptions
            with get() = x.GetCommaOptions KeyModelName KeyModelOptionsMapping KeyModelOptions.None (fun x y -> x ||| y)
            and set value = x.SetCommaOptions KeyModelName KeyModelOptionsMapping value Util.IsFlagSet
        member x.Magic
            with get() = _map.GetBoolValue MagicName
            and set value = _map.TrySetValue MagicName (SettingValue.ToggleValue value) |> ignore
        member x.MaxMapCount
            with get() = _map.GetNumberValue MaxMapCount
            and set value = _map.TrySetValue MaxMapCount (SettingValue.NumberValue value) |> ignore
        member x.MaxMapDepth
            with get() = _map.GetNumberValue MaxMapDepth
            and set value = _map.TrySetValue MaxMapDepth (SettingValue.NumberValue value) |> ignore
        member x.MouseModel 
            with get() = _map.GetStringValue MouseModelName
            and set value = _map.TrySetValue MouseModelName (SettingValue.StringValue value) |> ignore
        member x.Paragraphs
            with get() = _map.GetStringValue ParagraphsName
            and set value = _map.TrySetValue ParagraphsName (SettingValue.StringValue value) |> ignore
        member x.ScrollOffset
            with get() = _map.GetNumberValue ScrollOffsetName
            and set value = _map.TrySetValue ScrollOffsetName (SettingValue.NumberValue value) |> ignore
        member x.Sections
            with get() = _map.GetStringValue SectionsName
            and set value = _map.TrySetValue SectionsName (SettingValue.StringValue value) |> ignore
        member x.Selection
            with get() = _map.GetStringValue SelectionName
            and set value = _map.TrySetValue SelectionName (SettingValue.StringValue value) |> ignore
        member x.SelectionKind = x.SelectionKind
        member x.SelectMode 
            with get() = _map.GetStringValue SelectModeName
            and set value = _map.TrySetValue SelectModeName (SettingValue.StringValue value) |> ignore
        member x.SelectModeOptions 
            with get() = x.GetCommaOptions SelectModeName SelectModeOptionsMapping SelectModeOptions.None (fun x y -> x ||| y) 
            and set value = x.SetCommaOptions SelectModeName SelectModeOptionsMapping value Util.IsFlagSet
        member x.Shell 
            with get() = _map.GetStringValue ShellName
            and set value = _map.TrySetValue ShellName (SettingValue.StringValue value) |> ignore
        member x.ShellFlag
            with get() = _map.GetStringValue ShellFlagName
            and set value = _map.TrySetValue ShellFlagName (SettingValue.StringValue value) |> ignore
        member x.SmartCase
            with get() = _map.GetBoolValue SmartCaseName
            and set value = _map.TrySetValue SmartCaseName (SettingValue.ToggleValue value) |> ignore
        member x.StartOfLine 
            with get() = _map.GetBoolValue StartOfLineName
            and set value = _map.TrySetValue StartOfLineName (SettingValue.ToggleValue value) |> ignore
        member x.TildeOp
            with get() = _map.GetBoolValue TildeOpName
            and set value = _map.TrySetValue TildeOpName (SettingValue.ToggleValue value) |> ignore
        member x.Timeout
            with get() = _map.GetBoolValue TimeoutName
            and set value = _map.TrySetValue TimeoutName (SettingValue.ToggleValue value) |> ignore
        member x.TimeoutEx
            with get() = _map.GetBoolValue TimeoutExName
            and set value = _map.TrySetValue TimeoutExName (SettingValue.ToggleValue value) |> ignore
        member x.TimeoutLength
            with get() = _map.GetNumberValue TimeoutLengthName
            and set value = _map.TrySetValue TimeoutLengthName (SettingValue.NumberValue value) |> ignore
        member x.TimeoutLengthEx
            with get() = _map.GetNumberValue TimeoutLengthExName
            and set value = _map.TrySetValue TimeoutLengthExName (SettingValue.NumberValue value) |> ignore
        member x.UseEditorIndent
            with get() = _map.GetBoolValue UseEditorIndentName
            and set value = _map.TrySetValue UseEditorIndentName (SettingValue.ToggleValue value) |> ignore
        member x.UseEditorSettings
            with get() = _map.GetBoolValue UseEditorSettingsName
            and set value = _map.TrySetValue UseEditorSettingsName (SettingValue.ToggleValue value) |> ignore
        member x.VimRc 
            with get() = _map.GetStringValue VimRcName
            and set value = _map.TrySetValue VimRcName (SettingValue.StringValue value) |> ignore
        member x.VimRcPaths 
            with get() = _map.GetStringValue VimRcPathsName
            and set value = _map.TrySetValue VimRcPathsName (SettingValue.StringValue value) |> ignore
        member x.VirtualEdit
            with get() = _map.GetStringValue VirtualEditName
            and set value = _map.TrySetValue VirtualEditName (SettingValue.StringValue value) |> ignore
        member x.VisualBell
            with get() = _map.GetBoolValue VisualBellName
            and set value = _map.TrySetValue VisualBellName (SettingValue.ToggleValue value) |> ignore
        member x.WrapScan
            with get() = _map.GetBoolValue WrapScanName
            and set value = _map.TrySetValue WrapScanName (SettingValue.ToggleValue value) |> ignore
        member x.DisableAllCommand = _disableAllCommand
        member x.IsBackspaceEol = x.IsCommaSubOptionPresent BackspaceName "eol"
        member x.IsBackspaceIndent = x.IsCommaSubOptionPresent BackspaceName "indent"
        member x.IsBackspaceStart = x.IsCommaSubOptionPresent BackspaceName "start"
        member x.IsVirtualEditOneMore = x.IsCommaSubOptionPresent VirtualEditName "onemore"

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

type internal LocalSettings
    ( 
        _globalSettings : IVimGlobalSettings
    ) =

    static let LocalSettingInfo =
        [|
            (AutoIndentName, "ai", SettingValue.ToggleValue false)
            (ExpandTabName, "et", SettingValue.ToggleValue false)
            (NumberName, "nu", SettingValue.ToggleValue false)
            (NumberFormatsName, "nf", SettingValue.StringValue "octal,hex")
            (ShiftWidthName, "sw", SettingValue.NumberValue 8)
            (TabStopName, "ts", SettingValue.NumberValue 8)
            (QuoteEscapeName, "qe", SettingValue.StringValue @"\")
        |]

    let _map = SettingsMap(LocalSettingInfo, false)

    member x.Map = _map

    static member Copy (settings : IVimLocalSettings) = 
        let copy = LocalSettings(settings.GlobalSettings)
        settings.AllSettings
        |> Seq.filter (fun s -> not s.IsGlobal && not s.IsValueCalculated)
        |> Seq.iter (fun s -> copy.Map.TrySetValue s.Name s.Value |> ignore)
        copy :> IVimLocalSettings

    member x.IsNumberFormatSupported numberFormat =

        // The format is supported if the name is in the comma delimited value
        let isSupported format = 
            _map.GetStringValue NumberFormatsName
            |> StringUtil.split ','
            |> Seq.exists (fun value -> value = format)

        match numberFormat with
        | NumberFormat.Decimal ->
            // This is always supported independent of the option value
            true
        | NumberFormat.Octal ->
            isSupported "octal"
        | NumberFormat.Hex ->
            isSupported "hex"
        | NumberFormat.Alpha ->
            isSupported "alpha"

    interface IVimLocalSettings with 
        // IVimSettings
        
        member x.AllSettings = _map.AllSettings |> Seq.append _globalSettings.AllSettings
        member x.TrySetValue settingName value = 
            if _map.OwnsSetting settingName then _map.TrySetValue settingName value
            else _globalSettings.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = 
            if _map.OwnsSetting settingName then _map.TrySetValueFromString settingName strValue
            else _globalSettings.TrySetValueFromString settingName strValue
        member x.GetSetting settingName =
            if _map.OwnsSetting settingName then _map.GetSetting settingName
            else _globalSettings.GetSetting settingName

        member x.GlobalSettings = _globalSettings
        member x.AutoIndent
            with get() = _map.GetBoolValue AutoIndentName
            and set value = _map.TrySetValue AutoIndentName (SettingValue.ToggleValue value) |> ignore
        member x.ExpandTab
            with get() = _map.GetBoolValue ExpandTabName
            and set value = _map.TrySetValue ExpandTabName (SettingValue.ToggleValue value) |> ignore
        member x.Number
            with get() = _map.GetBoolValue NumberName
            and set value = _map.TrySetValue NumberName (SettingValue.ToggleValue value) |> ignore
        member x.NumberFormats
            with get() = _map.GetStringValue NumberFormatsName
            and set value = _map.TrySetValue NumberFormatsName (SettingValue.StringValue value) |> ignore
        member x.ShiftWidth  
            with get() = _map.GetNumberValue ShiftWidthName
            and set value = _map.TrySetValue ShiftWidthName (SettingValue.NumberValue value) |> ignore
        member x.TabStop
            with get() = _map.GetNumberValue TabStopName
            and set value = _map.TrySetValue TabStopName (SettingValue.NumberValue value) |> ignore
        member x.QuoteEscape
            with get() = _map.GetStringValue QuoteEscapeName
            and set value = _map.TrySetValue QuoteEscapeName (SettingValue.StringValue value) |> ignore

        member x.IsNumberFormatSupported numberFormat = x.IsNumberFormatSupported numberFormat

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

type internal WindowSettings
    ( 
        _globalSettings : IVimGlobalSettings,
        _textView : ITextView option
    ) as this =

    static let WindowSettingInfo =
        [|
            (CursorLineName, "cul", SettingValue.ToggleValue false)
            (ScrollName, "scr", SettingValue.NumberValue 25)
        |]

    let _map = SettingsMap(WindowSettingInfo, false)

    do
        let setting = _map.GetSetting ScrollName |> Option.get
        _map.ReplaceSetting ScrollName {
            setting with 
                Value = SettingValue.CalculatedNumber(this.CalculateScroll); 
                DefaultValue = SettingValue.CalculatedNumber(this.CalculateScroll) }

    new (settings) = WindowSettings(settings, None)
    new (settings, textView : ITextView) = WindowSettings(settings, Some textView)

    member x.Map = _map

    /// Calculate the scroll value as specified in the Vim documentation.  Should be half the number of 
    /// visible lines 
    member x.CalculateScroll() =
        let defaultValue = 10
        match _textView with
        | None -> defaultValue
        | Some textView ->
            try
                let col = textView.TextViewLines
                match col.FirstVisibleLine,col.LastVisibleLine with
                | (null, _) -> defaultValue
                | (_, null) -> defaultValue
                | (top, bottom) ->
                    let topLine = top.Start.GetContainingLine()
                    let endLine = bottom.End.GetContainingLine()
                    (endLine.LineNumber - topLine.LineNumber) / 2
            with 
                // This will be thrown if we're currently in the middle of an inner layout
                :? System.InvalidOperationException -> defaultValue

    static member Copy (settings : IVimWindowSettings) = 
        let copy = WindowSettings(settings.GlobalSettings)
        settings.AllSettings
        |> Seq.filter (fun s -> not s.IsGlobal && not s.IsValueCalculated)
        |> Seq.iter (fun s -> copy.Map.TrySetValue s.Name s.Value |> ignore)
        copy :> IVimWindowSettings

    interface IVimWindowSettings with 
        member x.AllSettings = _map.AllSettings |> Seq.append _globalSettings.AllSettings
        member x.TrySetValue settingName value = 
            if _map.OwnsSetting settingName then _map.TrySetValue settingName value
            else _globalSettings.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = 
            if _map.OwnsSetting settingName then _map.TrySetValueFromString settingName strValue
            else _globalSettings.TrySetValueFromString settingName strValue
        member x.GetSetting settingName =
            if _map.OwnsSetting settingName then _map.GetSetting settingName
            else _globalSettings.GetSetting settingName
        member x.GlobalSettings = _globalSettings

        member x.CursorLine 
            with get() = _map.GetBoolValue CursorLineName
            and set value = _map.TrySetValue CursorLineName (SettingValue.ToggleValue value) |> ignore
        member x.Scroll 
            with get() = _map.GetNumberValue ScrollName
            and set value = _map.TrySetValue ScrollName (SettingValue.NumberValue value) |> ignore

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

/// Certain changes need to be synchronized between the editor, local and global 
/// settings.  This MEF component takes care of that synchronization 
[<Export(typeof<IVimBufferCreationListener>)>]
type internal EditorToSettingSynchronizer
    [<ImportingConstructor>]
    (
        _editorOptionsFactoryService : IEditorOptionsFactoryService,
        _vim : IVim
    ) =

    let _globalSettings = _vim.GlobalSettings
    let _syncronizingSet = System.Collections.Generic.HashSet<IVimLocalSettings>()

    member x.VimBufferCreated (buffer : IVimBuffer) = 
        let editorOptions = buffer.TextView.Options
        if editorOptions <> null then

            let bag = DisposableBag()
            let localSettings = buffer.LocalSettings

            // Raised when a local setting is changed.  We need to inspect this setting and 
            // determine if it's an interesting setting and if so synchronize it with the 
            // editor options
            //
            // Cast up to IVimSettings to avoid the F# bug of accessing a CLIEvent from 
            // a derived interface
            (localSettings :> IVimSettings).SettingChanged 
            |> Observable.filter (fun args -> x.IsTrackedLocalSetting args.Setting)
            |> Observable.subscribe (fun _ -> x.TrySyncLocalToEditor localSettings editorOptions)
            |> bag.Add

            /// Raised when an editor option is changed.  If it's one of the values we care about
            /// then we need to sync to the local settings
            editorOptions.OptionChanged
            |> Observable.filter (fun e -> x.IsTrackedEditorSetting e.OptionId)
            |> Observable.subscribe (fun _ -> x.TrySyncEditorToLocal localSettings editorOptions)
            |> bag.Add

            // Finally we need to clean up our listeners when the buffer is closed.  At
            // that point synchronization is no longer needed
            buffer.Closed
            |> Observable.add (fun _ -> bag.DisposeAll())

            // Next we do the initial sync between editor and local settings
            if _globalSettings.UseEditorSettings then
                x.TrySyncEditorToLocal localSettings editorOptions
            else
                x.TrySyncLocalToEditor localSettings editorOptions

    /// Is this a local setting of note
    member x.IsTrackedLocalSetting (setting : Setting) = 
        if setting.Name = LocalSettingNames.TabStopName then
            true
        elif setting.Name = LocalSettingNames.ShiftWidthName then
            true
        elif setting.Name = LocalSettingNames.ExpandTabName then
            true
        elif setting.Name = LocalSettingNames.NumberName then
            true
        else
            false

    /// Is this an editor setting of note
    member x.IsTrackedEditorSetting optionId =
        if optionId = DefaultOptions.TabSizeOptionId.Name then
            true
        elif optionId = DefaultOptions.IndentSizeOptionId.Name then
            true
        elif optionId = DefaultOptions.ConvertTabsToSpacesOptionId.Name then
            true
        elif optionId = DefaultTextViewHostOptions.LineNumberMarginId.Name then
            true
        else
            false

    /// Synchronize the settings if needed.  Prevent recursive sync's here
    member x.TrySync localSettings syncFunc = 
        if _syncronizingSet.Add(localSettings) then
            try
                syncFunc()
            finally
                _syncronizingSet.Remove(localSettings) |> ignore

    /// Synchronize the settings from the editor to the local settings.  Do not
    /// call this directly but instead call through SynchronizeSettings
    member x.TrySyncLocalToEditor (localSettings : IVimLocalSettings) editorOptions =
        x.TrySync localSettings (fun () ->
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.TabSizeOptionId localSettings.TabStop
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.IndentSizeOptionId localSettings.ShiftWidth
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.ConvertTabsToSpacesOptionId localSettings.ExpandTab
            EditorOptionsUtil.SetOptionValue editorOptions DefaultTextViewHostOptions.LineNumberMarginId localSettings.Number)

    /// Synchronize the settings from the local settings to the editor.  Do not
    /// call this directly but instead call through SynchronizeSettings
    member x.TrySyncEditorToLocal (localSettings : IVimLocalSettings) editorOptions =
        x.TrySync localSettings (fun () ->
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.TabSizeOptionId with
            | None -> ()
            | Some tabSize -> localSettings.TabStop <- tabSize
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.IndentSizeOptionId with
            | None -> ()
            | Some shiftWidth -> localSettings.ShiftWidth <- shiftWidth
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.ConvertTabsToSpacesOptionId with
            | None -> ()
            | Some convertTabToSpace -> localSettings.ExpandTab <- convertTabToSpace
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultTextViewHostOptions.LineNumberMarginId with
            | None -> ()
            | Some show -> localSettings.Number <- show)

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.VimBufferCreated buffer