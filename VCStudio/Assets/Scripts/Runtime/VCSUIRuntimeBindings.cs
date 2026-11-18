using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class VCSUIRuntimeBindings
{
    public static void Bind(MonoBehaviour owner, string buttonName, UnityAction callback)
    {
        Button button = FindButton(owner, buttonName);
        if (button == null) return;
        if (!HasValidPersistentListener(button))
        {

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
            Debug.Log("[VCS/UI] Bound " + buttonName + " -> " + callback.Method.Name, owner);
        }
    }

    public static void BindChoice(MonoBehaviour owner, string buttonName, UnityAction<int> callback, int index)
    {
        Button button = FindButton(owner, buttonName);
        if (button == null) return;
        if (!HasValidPersistentListener(button))
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback(index));
            Debug.Log("[VCS/UI] Bound " + buttonName + " -> ChooseAvatar(" + index + ")", owner);
        }
    }

    private static Button FindButton(MonoBehaviour owner, string name)
    {
        Button[] buttons = owner.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null && buttons[i].gameObject.name == name)
                return buttons[i];
        Debug.LogError("[VCS/UI] Missing Button '" + name + "' under " + owner.name +
                       ". Check the generated menu hierarchy.", owner);
        return null;
    }

    private static bool HasValidPersistentListener(Button button)
    {
        int n = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < n; i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null &&
                !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
                return true;
        }
        return false;
    }

    public static void EnsureInputModule(MonoBehaviour owner)
    {

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        EventSystem es = UnityEngine.Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            Debug.LogError("[VCS/UI] No EventSystem in scene.", owner);
            return;
        }
        Type newModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newModuleType == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                newModuleType = assembly.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (newModuleType != null) break;
            }
        }
        if (newModuleType == null)
        {
            Debug.LogError("[VCS/UI] New Input System is active but InputSystemUIInputModule " +
                "was not found. Install/enable the Input System package.", owner);
            return;
        }
        BaseInputModule module = es.GetComponent(newModuleType) as BaseInputModule;
        if (module == null) module = es.gameObject.AddComponent(newModuleType) as BaseInputModule;
        if (module == null)
        {
            Debug.LogError("[VCS/UI] Could not add InputSystemUIInputModule.", owner);
            return;
        }

        var assign = newModuleType.GetMethod("AssignDefaultActions", System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (assign != null)
        {
            try { assign.Invoke(module, null); }
            catch (Exception e) { Debug.LogWarning("[VCS/UI] AssignDefaultActions: " + e.Message, owner); }
        }
        foreach (StandaloneInputModule legacy in es.GetComponents<StandaloneInputModule>())
            legacy.enabled = false;
        module.enabled = true;
        Debug.Log("[VCS/UI] EventSystem uses InputSystemUIInputModule (New Input System).", owner);
#endif
    }
}
