using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class Window : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public static readonly Dictionary<Type, HashSet<Window>> instancesByType = new();

    // Focus/render order across all open windows: the last entry is the topmost.
    private static readonly List<Window> zOrder = new();

    public bool isMouseOver;

    // Persistent windows (e.g. the Character Finder, which is toggled rather than
    // destroyed) opt out of closing via the Escape fallback.
    protected virtual bool CloseableByEscape => true;

    protected virtual void Awake()
    {
        Type t = GetType();

        if (!instancesByType.TryGetValue(t, out var set))
        {
            set = new HashSet<Window>();
            instancesByType[t] = set;
        }

        set.Add(this);
        zOrder.Add(this);
    }

    protected virtual void OnDestroy()
    {
        Type t = GetType();

        zOrder.Remove(this);

        if (instancesByType.TryGetValue(t, out var set))
        {
            set.Remove(this);

            if (set.Count == 0)
                instancesByType.Remove(t);
        }
    }

    // Marks this window as topmost. Called when it is dragged or opened.
    public void BringToFront()
    {
        zOrder.Remove(this);
        zOrder.Add(this);
    }

    // Keyboard fallback: closes the topmost visible, dismissible window.
    // Returns true if any window was closed.
    public static bool CloseTopmostWindow()
    {
        for (int i = zOrder.Count - 1; i >= 0; i--)
        {
            Window window = zOrder[i];
            if (window == null)
            {
                zOrder.RemoveAt(i); // clear stale references
                continue;
            }
            if (!window.gameObject.activeInHierarchy) continue; // hidden, not "open"
            if (!window.CloseableByEscape) continue;            // persistent, not dismissible

            window.CloseWindow();
            return true;
        }
        return false;
    }

    public void CloseWindow()
    {
        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData) => isMouseOver = true;
    public void OnPointerExit(PointerEventData eventData) => isMouseOver = false;

    public static bool HasWindow<TWindow, TTarget>(TTarget target)
    where TWindow : Window<TTarget>
    {
        if (!instancesByType.TryGetValue(typeof(TWindow), out var set))
            return false;

        foreach (var window in set)
        {
            if (((TWindow)window).Target?.Equals(target) == true)
                return true;
        }

        return false;
    }

}


public abstract class Window<T> : Window
{
    public T Target; // Strongly typed Target for windows that need one

}