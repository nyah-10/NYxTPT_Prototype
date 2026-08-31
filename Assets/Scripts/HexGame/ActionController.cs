using UnityEngine;

public class ActionController : MonoBehaviour
{
    public int MainActionPoint { get; private set; }
    public int SubActionPoint { get; private set; }

    private void Awake()
    {
        StartTurn();
    }

    public void StartTurn()
    {
        MainActionPoint = 1;
        SubActionPoint = 1;
    }

    public bool UseMoveAction()
    {
        if (SubActionPoint <= 0)
        {
            Debug.Log("Not enough Sub Action Points to move.");
            return false;
        }

        SubActionPoint--;
        return true;
    }

    public bool UseMainSkill()
    {
        if (MainActionPoint <= 0)
        {
            Debug.Log("Not enough Main Action Points to use a skill.");
            return false;
        }

        MainActionPoint--;
        return true;
    }
}
