﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FaderPlugin.Config;
using ImGuiNET;

namespace FaderPlugin {
    public class PluginUI : IDisposable {
        private enum OverrideKeys {
            Alt = 0x12,
            Ctrl = 0x11,
            Shift = 0x10,
        }

        private Configuration configuration;
        private Element selectedElement;
        private List<ConfigEntry> selectedConfig;

        private bool settingsVisible = false;

        public bool SettingsVisible {
            get { return settingsVisible; }
            set { settingsVisible = value; }
        }

        private Vector2 _windowSize = new Vector2(1400, 850) * ImGui.GetIO().FontGlobalScale;
        private OverrideKeys CurrentOverrideKey => (OverrideKeys)configuration.OverrideKey;
        private HttpClient _httpClient = new HttpClient();
        private string _noticeString;
        private string _noticeUrl;

        public PluginUI(Configuration configuration) {
            this.configuration = configuration;

            DownloadAndParseNotice();
        }

        private void DownloadAndParseNotice() {
            try {
                var stringAsync = _httpClient.GetStringAsync("https://shdwp.github.io/ukraine/xiv_notice.txt");
                stringAsync.Wait();
                var strArray = stringAsync.Result.Split('|');

                if((uint)strArray.Length > 0U) {
                    _noticeString = strArray[0];
                }

                if(strArray.Length <= 1) {
                    return;
                }

                _noticeUrl = strArray[1];

                if(!(_noticeUrl.StartsWith("http://") || _noticeUrl.StartsWith("https://"))) {
                    // PluginLog.Warning($"Received invalid noticeUrl {_noticeUrl}, ignoring");
                    _noticeUrl = null;
                }
            } catch(Exception ex) {
            }
        }

        private void DisplayNotice() {
            if(_noticeString == null) {
                return;
            }

            ImGui.Dummy(new Vector2(0.0f, 15f));
            ImGui.PushStyleColor((ImGuiCol)0, ImGuiColors.DPSRed);
            ImGuiHelpers.SafeTextWrapped(_noticeString);

            if(_noticeUrl != null) {
                if(ImGui.Button(_noticeUrl)) {
                    try {
                        Process.Start(new ProcessStartInfo {
                            FileName = _noticeUrl,
                            UseShellExecute = true
                        });
                    } catch(Exception ex) {
                    }
                }
            }

            ImGui.PopStyleColor();
        }

        public void Dispose() {
        }

        public void Draw() {
            if(!SettingsVisible) {
                return;
            }

            DrawSettingsWindow();
        }

        public void DrawSettingsWindow() {
            ImGui.SetNextWindowSize(_windowSize, ImGuiCond.Always);

            if(ImGui.Begin("Fader Plugin Configuration", ref settingsVisible)) {
                DisplayNotice();

                ImGui.Text("User Focus key:");
                ImGuiHelpTooltip("When held interface will be setup as per 'UserFocus' column.");

                if(ImGui.BeginCombo("", CurrentOverrideKey.ToString())) {
                    foreach(var option in Enum.GetValues(typeof(OverrideKeys))) {
                        if(ImGui.Selectable(option.ToString(), option.Equals(CurrentOverrideKey))) {
                            configuration.OverrideKey = (int)option;
                            configuration.Save();
                        }
                    }

                    ImGui.EndCombo();
                }

                var focusOnHotbarsUnlock = configuration.FocusOnHotbarsUnlock;
                if(ImGui.Checkbox("##focus_on_unlocked_bars", ref focusOnHotbarsUnlock)) {
                    configuration.FocusOnHotbarsUnlock = focusOnHotbarsUnlock;
                    configuration.Save();
                }

                ImGui.SameLine();
                ImGui.Text("Always User Focus when hotbars are unlocked");
                ImGuiHelpTooltip("When hotbars or crossbars are unlocked always setup to the UserFocus column.");

                var idleDelay = (float)TimeSpan.FromMilliseconds(configuration.IdleTransitionDelay).TotalSeconds;
                ImGui.Text("Idle transition delay:");
                ImGui.SameLine();
                if(ImGui.SliderFloat("##idle_delay", ref idleDelay, 0.1f, 15f)) {
                    configuration.IdleTransitionDelay = (long)TimeSpan.FromSeconds(idleDelay).TotalMilliseconds;
                    configuration.Save();
                }

                ImGuiHelpTooltip("Amount of time it takes to go back to the `Idle` column.");

                ImGui.Separator();

                ImGui.Columns(2, "columns", false);

                var buttonWidth = ImGui.CalcTextSize("Status Enfeeblements   ?").X + 10;

                ImGui.SetColumnWidth(0, buttonWidth + 40);

                ImGui.BeginChild("##elementsList");
                foreach(Element element in Enum.GetValues(typeof(Element))) {
                    if(ShouldIgnoreElement(element)) {
                        continue;
                    }

                    var buttonText = ElementUtil.GetElementName(element);
                    string tooltipText = TooltipForElement(element);
                    if(tooltipText != null) {
                        buttonText += "   ?";
                    }

                    ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
                    if(ImGui.Button(buttonText, new Vector2(buttonWidth, 22))) {
                        selectedElement = element;

                        selectedConfig = configuration.GetElementConfig(element);
                    }

                    if(tooltipText != null) {
                        if(ImGui.IsItemHovered()) {
                            ImGui.SetTooltip(tooltipText);
                        }
                    }

                }
                ImGui.EndChild();

                ImGui.NextColumn();

                // Config for the selected elements.
                if(selectedElement != Element.Unknown) {
                    string elementName = ElementUtil.GetElementName(selectedElement);
                    ImGui.Text($"{elementName} Configuration");

                    // Config for each condition.
                    for(int i = 0; i < selectedConfig.Count; i++) {
                        var elementState = selectedConfig[i].state;
                        var elementSetting = selectedConfig[i].setting;

                        // State
                        ImGui.SetNextItemWidth(200);
                        if(ImGui.BeginCombo($"##{elementName}-{i}-state", elementState.ToString())) {
                            foreach(State state in Enum.GetValues(typeof(State))) {
                                if(state == State.None || state == State.Default) {
                                    continue;
                                }
                                if(ImGui.Selectable(StateUtil.GetStateName(state))) {
                                    selectedConfig[i].state = state;
                                    configuration.Save();
                                }
                            }
                            ImGui.EndCombo();
                        }

                        // Setting
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(200);
                        if(ImGui.BeginCombo($"##{elementName}-{i}-setting", elementSetting.ToString())) {
                            foreach(Setting setting in Enum.GetValues(typeof(Setting))) {
                                if(ImGui.Selectable(setting.ToString())) {
                                    selectedConfig[i].setting = setting;
                                    configuration.Save();
                                }
                            }
                            ImGui.EndCombo();
                        }

                        if(elementState == State.Default) {
                            continue;
                        }

                        // Up
                        ImGui.SameLine();
                        ImGui.PushFont(UiBuilder.IconFont);
                        if(ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}##{elementName}-{i}-up")) {
                            if(i > 0) {
                                var swap1 = selectedConfig[i - 1];
                                var swap2 = selectedConfig[i];

                                if(swap1.state != State.Default && swap2.state != State.Default) {
                                    selectedConfig[i] = swap1;
                                    selectedConfig[i - 1] = swap2;

                                    configuration.Save();
                                }
                            }
                        }

                        // Down
                        ImGui.SameLine();
                        if(ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}##{elementName}-{i}-down")) {
                            if(i < selectedConfig.Count - 1) {
                                var swap1 = selectedConfig[i + 1];
                                var swap2 = selectedConfig[i];

                                if(swap1.state != State.Default && swap2.state != State.Default) {
                                    selectedConfig[i] = swap1;
                                    selectedConfig[i + 1] = swap2;

                                    configuration.Save();
                                }
                            }
                        }

                        // Delete
                        ImGui.SameLine();
                        if(ImGui.Button($"{FontAwesomeIcon.TrashAlt.ToIconString()}##{elementName}-{i}-delete")) {
                            selectedConfig.RemoveAt(i);

                            configuration.Save();
                        }
                        ImGui.PopFont();
                    }

                    ImGui.PushFont(UiBuilder.IconFont);
                    if(ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##{elementName}-add")) {
                        // Add the new state then swap it with the existing default state.
                        selectedConfig.Add(new(State.None, Setting.Hide));
                        var swap1 = selectedConfig[^1];
                        var swap2 = selectedConfig[^2];

                        selectedConfig[^2] = swap1;
                        selectedConfig[^1] = swap2;

                        configuration.Save();
                    }
                    ImGui.PopFont();
                }

            }

            _windowSize = ImGui.GetWindowSize();
            ImGui.End();
        }

        private void ImGuiHelpTooltip(string tooltip) {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "?");
            if(ImGui.IsItemHovered()) {
                ImGui.SetTooltip(tooltip);
            }
        }

        private string TooltipForState(State state) {
            return state switch {
                State.Combat => "In combat",
                State.Crafting => "Crafting an item",
                State.Duty => "In instanced duty",
                State.Gathering => "Gathering a node",
                State.ChatFocus => "When typing a message in chat",
                State.UserFocus => "Focus button pressed",
                State.EnemyTarget => "Targeting an enemy",
                State.PlayerTarget => "Targeting a player",
                State.NPCTarget => "Targeting a NPC",
                State.Default => "When other conditions are not active",
                _ => "No tooltip",
            };
        }

        private string TooltipForElement(Element elementId) {
            return elementId switch {
                Element.Chat =>
                    "Should be always visible if focused, albeit feature can be buggy with some configurations",
                Element.Job => "Job-specific UI",
                Element.Status => "Player status (when not split into 3 separate elements)",
                Element.StatusEnfeeblements => "Player enfeeblements (when split into 3 separate elements)",
                Element.StatusEnhancements => "Player enhancements (when split into 3 separate elements)",
                Element.StatusOther => "Player other status (when split into 3 separate elements)",
                _ => null,
            };
        }

        private bool ShouldIgnoreElement(Element elementId) {
            return elementId switch {
                Element.QuestLog => true,
                Element.Nameplates => true,
                Element.Unknown => true,
                _ => false,
            };
        }
    }
}