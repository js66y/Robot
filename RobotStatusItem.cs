using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotStatusItem : MonoBehaviour
{
    public Image statusIcon;
    public TextMeshProUGUI statusText;
    public Robot targetRobot;
    
    public void Initialize(Robot robot)
    {
        targetRobot = robot;
    }
    
    public void UpdateDisplay()
    {
        if (targetRobot == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        statusIcon.color = targetRobot.color;
        
        string status = "空闲";
        if (targetRobot.hasTask)
        {
            status = targetRobot.hasCargo ? "运送货物" : "前往货架";
        }
        
        statusText.text = $"Robot-{targetRobot.GetInstanceID() % 1000}: {status}";
    }
}