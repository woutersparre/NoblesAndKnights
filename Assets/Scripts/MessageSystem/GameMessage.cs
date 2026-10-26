using System.Collections.Generic;

public enum MessagePriority { Popup, High, Low, Log }
public enum MessageCategory { Diplomacy, Dynasty, Intrigue, Militairy, Government, Religion, Other }

public class GameMessage
{
    public string Text { get; }
    public MessagePriority Priority { get; }
    public MessageCategory Category { get; }

    public int Day { get; }

    public Dictionary<string, Character> characterLinks = new();

    public GameMessage(string template, MessagePriority priority, MessageCategory category, int day, params object[] arguments)
    {
        Priority = priority;
        Category = category;
        Day = day;
        Text = Format(template, arguments);
    }

    private string Format(string template, object[] arguments)
    {
        string formattedText = template;

        for (int i = 0; i < arguments.Length; i++)
        {
            object arg = arguments[i];
            string replacement;

            // Characters are rendered as clickable TMP links; other args render as plain text
            if (arg is Character character)
            {
                string characterName = character.Name;
                characterLinks[characterName] = character;
                replacement = $"<link={characterName}><color=blue>{characterName}</color></link>";
            }
            else
            {
                replacement = arg?.ToString() ?? "";
            }

            formattedText = formattedText.Replace("{" + i + "}", replacement);
        }

        return formattedText;
    }
}