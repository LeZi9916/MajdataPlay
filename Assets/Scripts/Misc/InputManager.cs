using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public Camera mainCamera;
    public bool AutoPlay = false;

    Guid guid = Guid.NewGuid();
    public List<Sensor> triggerSensors = new();
    List<GameObject> sensors = new();
    SensorManager sManager;
    // Start is called before the first frame update
    void Start()
    {
        
        var sManagerObj = GameObject.Find("Sensors");
        var count = sManagerObj.transform.childCount;
        for (int i = 0; i < count; i++)
            sensors.Add(sManagerObj.transform.GetChild(i).gameObject);
        sManager = sManagerObj.GetComponent<SensorManager>();
    }

    // Update is called once per frame
    void Update()
    {
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
