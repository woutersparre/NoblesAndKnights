using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MessageManager : MonoBehaviour
{
    public static MessageManager instance;
    public GameObject highPriorityPopup;

    public Queue<GameMessage> highPriorityQueue = new();
    public Queue<GameMessage> lowPriorityQueue = new();

    public UnityEvent OnMessageQueueChanged = new();

    private void Awake()
    {
        instance = this;
    }

    public void Send(string message, params object[] arguments)
    {
        Send(MessagePriority.High, MessageCategory.Other, message, arguments);
    }

    public void Send(MessagePriority priority, string message, params object[] arguments)
    {
        Send(priority, MessageCategory.Other, message, arguments);
    }

    public void Send(MessagePriority priority, MessageCategory category, string message, params object[] arguments)
    {
        int day = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
        GameMessage gameMessage = new GameMessage(message, priority, category, day, arguments);

        switch (priority)
        {
            case MessagePriority.Popup:
                // Popup UI not implemented yet
                break;

            case MessagePriority.High:
                highPriorityQueue.Enqueue(gameMessage);
                break;

            case MessagePriority.Low:
                lowPriorityQueue.Enqueue(gameMessage);
                break;

            case MessagePriority.Log:
                Debug.Log(gameMessage.Text);
                break;

            default:
                Debug.LogWarning("Unknown priority level: " + priority);
                break;
        }

        OnMessageQueueChanged.Invoke();
    }
}