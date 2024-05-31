using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Sensor;

public class InputManager : MonoBehaviour
{
    public Camera mainCamera;
    public bool AutoPlay = false;
    public event Action<SensorType, SensorStatus, SensorStatus> OnSensorStatusChange;//oStatus nStatus

    Guid guid = Guid.NewGuid();
    public List<Sensor> triggerSensors = new();
    List<GameObject> sensors = new();
    SensorManager sManager;

    Dictionary<KeyCode, SensorStatus> keyRecorder = new();
    Dictionary<KeyCode, SensorType> keyMap = new();
    // Start is called before the first frame update
    void Start()
    {
        var sManagerObj = GameObject.Find("Sensors");
        var count = sManagerObj.transform.childCount;
        for (int i = 0; i < count; i++)
            sensors.Add(sManagerObj.transform.GetChild(i).gameObject);
        sManager = sManagerObj.GetComponent<SensorManager>();
        keyMap.Add(KeyCode.LeftArrow, SensorType.A1);
        keyMap.Add(KeyCode.RightArrow, SensorType.A1);
    }

    // Update is called once per frame
    void Update()
    {
        var count = Input.touchCount;
        CheckKey(new KeyCode[] {KeyCode.LeftArrow , KeyCode.RightArrow});

        if (Input.GetKeyDown(KeyCode.Home))
            AutoPlay = !AutoPlay;
        if (Input.GetMouseButton(0))
        {
            Vector3 screenPosition = Input.mousePosition;
            screenPosition.z = mainCamera.nearClipPlane;
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0;
            Running(worldPosition);
        }
        else
        {
            if (triggerSensors.Count == 0)
                return;
            foreach (var s in triggerSensors)
                sManager.SetSensorOff(s.Type, guid);
            triggerSensors.Clear();
        }

    }
    void CheckKey(KeyCode[] keys)
    {
        var dict = keys.ToDictionary(x => x,x => Input.GetKeyDown(x));
        foreach(var key in dict.Keys)
        {
            if (keyMap.ContainsKey(key) && OnSensorStatusChange != null && dict[key])
            {
                OnSensorStatusChange(keyMap[key], SensorStatus.Off, SensorStatus.On);
                sManager.GetSensor(keyMap[key]).IsJudging = false;
            }
        }
    }
    void Running(Vector3 pos)
    {
        var starRadius = 0.763736616f;
        var starPos = pos;
        var oldList = new List<Sensor>(triggerSensors);
        triggerSensors.Clear();
        foreach (var s in sensors.Select(x => x.GetComponent<RectTransform>()))
        {
            var sensor = s.GetComponent<Sensor>();
            if (sensor.Group == Sensor.SensorGroup.E || sensor.Group == Sensor.SensorGroup.D)
                continue;

            var rCenter = s.position;
            var rWidth = s.rect.width * s.lossyScale.x;
            var rHeight = s.rect.height * s.lossyScale.y;

            var radius = Math.Max(rWidth, rHeight) / 2;

            if ((starPos - rCenter).sqrMagnitude <= (radius * radius + starRadius * starRadius))
                triggerSensors.Add(sensor);
        }
        var untriggerSensors = oldList.Where(x => !triggerSensors.Contains(x));

        foreach (var s in untriggerSensors)
            sManager.SetSensorOff(s.Type, guid);
        foreach (var s in triggerSensors)
            sManager.SetSensorOn(s.Type, guid);
    }
}
