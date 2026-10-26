using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MessagesUI : MonoBehaviour, IPointerClickHandler
{
    // Background highlight drawn behind the name of the link the cursor is over.
    const string HighlightOpen = "<mark=#FFD70066>";
    const string HighlightClose = "</mark>";

    public TMP_Text HighPrioMessageCounter, LowPrioMessageCounter, MessageText, Titel, Date;
    public GameObject MessageScreen;

    Queue<GameMessage> currentQueue;

    // MessageText as stored by the message, without the hover highlight, and
    // the link id the cursor is currently over (null when over no link).
    string baseText;
    string hoveredLinkId;

    private void Awake()
    {
        MessageScreen.SetActive(false);
        MessageManager.instance.OnMessageQueueChanged.AddListener(UpdateCounters);
        UpdateCounters();
    }

    void Update()
    {
        if (MessageText == null)
            return;

        bool canHover = MessageScreen != null && MessageScreen.activeSelf && Mouse.current != null;
        SetHoveredLinkId(canHover ? LinkIdAtPosition(Mouse.current.position.ReadValue()) : null);
    }

    public void UpdateCounters()
    {
        HighPrioMessageCounter.text =
            MessageManager.instance.highPriorityQueue.Count.ToString();

        LowPrioMessageCounter.text =
            MessageManager.instance.lowPriorityQueue.Count.ToString();
    }

    public void OpenHighPriority()
    {
        // Tapping the button while its screen is already showing closes it
        // again (toggle), instead of just refreshing the same queue.
        if (MessageScreen.activeSelf && currentQueue == MessageManager.instance.highPriorityQueue)
        {
            MessageScreen.SetActive(false);
            return;
        }

        currentQueue = MessageManager.instance.highPriorityQueue;
        OpenScreen();
    }

    public void OpenLowPriority()
    {
        // Tapping the button while its screen is already showing closes it
        // again (toggle), instead of just refreshing the same queue.
        if (MessageScreen.activeSelf && currentQueue == MessageManager.instance.lowPriorityQueue)
        {
            MessageScreen.SetActive(false);
            return;
        }

        currentQueue = MessageManager.instance.lowPriorityQueue;
        OpenScreen();
    }

    void OpenScreen()
    {
        UpdateCounters();

        if (currentQueue == null || currentQueue.Count == 0)
        {
            MessageScreen.SetActive(false);
            return;
        }

        UpdateMessage(currentQueue.Peek());
        MessageScreen.SetActive(true);
    }

    void UpdateMessage(GameMessage message)
    {
        baseText = message.Text;
        hoveredLinkId = null;
        MessageText.text = baseText;
        Titel.text = message.Category.ToString();
        if (Date != null)
            Date.text = "Day: " + message.Day;
    }

    public void DismissMessage()
    {
        if (currentQueue == null || currentQueue.Count == 0)
            return;

        currentQueue.Dequeue();
        OpenScreen();
    }

    /// <summary>
    /// Clicking a linked character name in the message text opens that
    /// character's window. The EventSystem hands the click to the first
    /// ancestor implementing the handler, which is this component, since
    /// MessageText lives under the "Messages" object.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentQueue == null || currentQueue.Count == 0)
            return;

        string linkId = LinkIdAtPosition(eventData.position);
        if (linkId == null)
            return;

        currentQueue.Peek().characterLinks.TryGetValue(linkId, out Character character);

        if (character != null && GameUIManager.instance != null)
            GameUIManager.NewCharacterUI(character);
    }

    // Rewrites MessageText when the link under the cursor changes, so the
    // hovered link gets a highlight and everything else keeps its base text.
    void SetHoveredLinkId(string linkId)
    {
        if (linkId == hoveredLinkId)
            return;

        hoveredLinkId = linkId;

        if (baseText == null)
            return;

        MessageText.text = linkId == null ? baseText : WithHighlight(baseText, linkId);
    }

    string LinkIdAtPosition(Vector3 screenPosition)
    {
        if (MessageText == null)
            return null;

        // The GameUI canvas uses Screen Space Overlay, so the camera
        // argument stays null (required for overlay hit testing).
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(MessageText, screenPosition, null);
        if (linkIndex == -1)
            return null;

        return MessageText.textInfo.linkInfo[linkIndex].GetLinkID();
    }

    // Wraps the content of every <link=...> whose id matches linkId with a
    // mark tag. Rebuilt from baseText each time, so highlights never stack.
    static string WithHighlight(string text, string linkId)
    {
        const string linkOpen = "<link=";
        const string linkClose = "</link>";

        StringBuilder builder = new StringBuilder(text.Length + 32);
        int cursor = 0;

        while (true)
        {
            int open = text.IndexOf(linkOpen, cursor, StringComparison.Ordinal);
            if (open < 0)
                break;

            int tagEnd = text.IndexOf('>', open);
            if (tagEnd < 0)
                break;

            int contentStart = tagEnd + 1;
            int close = text.IndexOf(linkClose, contentStart, StringComparison.Ordinal);
            if (close < 0)
                break;

            string id = text.Substring(open + linkOpen.Length, tagEnd - open - linkOpen.Length);

            builder.Append(text, cursor, contentStart - cursor);
            if (id == linkId)
                builder.Append(HighlightOpen);
            builder.Append(text, contentStart, close - contentStart);
            if (id == linkId)
                builder.Append(HighlightClose);
            builder.Append(linkClose);

            cursor = close + linkClose.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }
}
