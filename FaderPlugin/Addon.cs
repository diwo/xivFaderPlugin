﻿using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FaderPlugin;

public static unsafe class Addon {
    private static readonly AtkStage* stage = AtkStage.Instance();

    private static Dictionary<string, (short, short)> storedPositions = new();
    private static Dictionary<string, bool> lastState = new();

    private static bool IsAddonOpen(string name) {
        nint addonPointer = Plugin.GameGui.GetAddonByName(name, 1);
        return addonPointer != nint.Zero;
    }

    public static bool HasAddonStateChanged(string name) {
        bool currentState = IsAddonOpen(name);
        bool changed = !lastState.ContainsKey(name) || lastState[name] != currentState;

        lastState[name] = currentState;

        return changed;
    }

    private static bool IsAddonFocused(string name) {
        foreach (var addon in stage->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList.Entries)
        {
            if (addon.Value == null || addon.Value->Name == null)
                continue;

            if (name.Equals(addon.Value->NameString))
                return true;
        }

        return false;
    }

    public static bool IsHudManagerOpen() {
        return IsAddonOpen("HudLayout");
    }

    public static bool HasHudManagerStateChanged() {
        return HasAddonStateChanged("HudLayout");
    }

    public static bool IsChatFocused()
    {
        // Check for ChatLogPanel_[0-3] as well to prevent chat from disappearing while user is scrolling through logs via controller input
        return IsAddonFocused("ChatLog")
               || IsAddonFocused("ChatLogPanel_0")
               || IsAddonFocused("ChatLogPanel_1")
               || IsAddonFocused("ChatLogPanel_2")
               || IsAddonFocused("ChatLogPanel_3");
    }

    public static bool AreHotbarsLocked() {
        var hotbar = Plugin.GameGui.GetAddonByName("_ActionBar", 1);
        var crossbar = Plugin.GameGui.GetAddonByName("_ActionCross", 1);

        if (hotbar == nint.Zero || crossbar == nint.Zero)
            return true;

        var hotbarAddon = (AddonActionBar*)hotbar;
        var crossbarAddon = (AddonActionCross*)hotbar;

        try {
            // Check whether Mouse Mode or Gamepad Mode is enabled.
            var mouseModeEnabled = hotbarAddon->ShowHideFlags == 0;
            return mouseModeEnabled ? hotbarAddon->IsLocked : crossbarAddon->IsLocked;
        } catch(AccessViolationException) {
            return true;
        }
    }

    public static void SetAddonVisibility(string name, bool isVisible) {
        nint addonPointer = Plugin.GameGui.GetAddonByName(name, 1);
        if(addonPointer == nint.Zero) {
            return;
        }

        AtkUnitBase* addon = (AtkUnitBase*)addonPointer;

        if(isVisible) {
            // Restore the elements position on screen.
            if (storedPositions.TryGetValue(name, out var position) && (addon->X == -9999 || addon->Y == -9999))
            {
                var (x, y) = position;
                addon->SetPosition(x, y);
            }
        } else {
            // Store the position prior to hiding the element.
            if(addon->X != -9999 && addon->Y != -9999) {
                storedPositions[name] = (addon->X, addon->Y);
            }

            // Move the element off screen so it can't be interacted with.
            addon->SetPosition(-9999, -9999);
        }
    }

    public static bool IsWeaponUnsheathed()
    {
        return UIState.Instance()->WeaponState.IsUnsheathed;
    }

    public static bool InSanctuary()
    {
        return TerritoryInfo.Instance()->InSanctuary;
    }
}