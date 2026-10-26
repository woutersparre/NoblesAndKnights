[System.Serializable]
public class OpinionModifier
{
    public string Reason;
    public int Value;
    public int YearsLeft;

    public OpinionModifier(string reason = "", int value = 0, int yearsLeft = -1)
    {
        Reason = reason;
        Value = value;
        YearsLeft = yearsLeft;
    }

    public bool IsActive => YearsLeft < 0 || YearsLeft > 0;
}