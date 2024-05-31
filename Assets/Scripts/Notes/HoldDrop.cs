﻿using System;
using System.Diagnostics;
using UnityEngine;
using static NoteEffectManager;
using static Sensor;
using static UnityEngine.EventSystems.EventTrigger;
using Random = System.Random;

public class HoldDrop : NoteLongDrop
{
    public int startPosition = 1;
    public float speed = 1;

    public bool isEach;
    public bool isEX;
    public bool isBreak;

    public Sprite tapSpr;
    public Sprite holdOnSpr;
    public Sprite holdOffSpr;
    public Sprite eachSpr;
    public Sprite eachHoldOnSpr;
    public Sprite exSpr;
    public Sprite breakSpr;
    public Sprite breakHoldOnSpr;

    public Sprite eachLine;
    public Sprite breakLine;

    public Sprite holdEachEnd;
    public Sprite holdBreakEnd;

    public RuntimeAnimatorController HoldShine;
    public RuntimeAnimatorController BreakShine;

    public GameObject holdEffect;

    public GameObject tapLine;
    public Material missMaterial;

    public Color exEffectTap;
    public Color exEffectEach;
    public Color exEffectBreak;
    private Animator animator;

    public Material breakMaterial;

    private bool breakAnimStart;
    private SpriteRenderer exSpriteRender;
    private bool holdAnimStart;
    private SpriteRenderer holdEndRender;
    private SpriteRenderer lineSpriteRender;

    private SpriteRenderer spriteRenderer;

    private AudioTimeProvider timeProvider;

    Guid guid = Guid.NewGuid();
    Sensor sensor;
    SensorManager manager;
    JudgeType headJudge;
    bool isJudged;
    NoteManager noteManager;
    InputManager inputManager;
    Stopwatch userHold = new();
    float lastHoldTiming = -1;

    private void Start()
    {
        var notes = GameObject.Find("Notes").transform;
        noteManager = notes.GetComponent<NoteManager>();
        holdEffect = Instantiate(holdEffect, notes);
        holdEffect.SetActive(false);

        tapLine = Instantiate(tapLine, notes);
        tapLine.SetActive(false);
        lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();

        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();

        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        holdEndRender = transform.GetChild(1).GetComponent<SpriteRenderer>();

        spriteRenderer.sortingOrder += noteSortOrder;
        exSpriteRender.sortingOrder += noteSortOrder;
        holdEndRender.sortingOrder += noteSortOrder;

        spriteRenderer.sprite = tapSpr;
        exSpriteRender.sprite = exSpr;

        var anim = gameObject.AddComponent<Animator>();
        anim.enabled = false;
        animator = anim;

        if (isEX) exSpriteRender.color = exEffectTap;
        if (isEach)
        {
            spriteRenderer.sprite = eachSpr;
            lineSpriteRender.sprite = eachLine;
            holdEndRender.sprite = holdEachEnd;
            if (isEX) exSpriteRender.color = exEffectEach;
        }

        if (isBreak)
        {
            spriteRenderer.sprite = breakSpr;
            lineSpriteRender.sprite = breakLine;
            holdEndRender.sprite = holdBreakEnd;
            if (isEX) exSpriteRender.color = exEffectBreak;
            spriteRenderer.material = breakMaterial;
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;
        holdEndRender.enabled = false;

        sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
        manager = GameObject.Find("Sensors")
                                .GetComponent<SensorManager>();
        inputManager = GameObject.Find("Input")
                                 .GetComponent<InputManager>();
        sensor.OnSensorStatusChange += Check;
        inputManager.OnSensorStatusChange += Check;
    }
    private void FixedUpdate()
    {
        var timing = timeProvider.AudioTime - time;
        var holdTime = timing - LastFor;
        if (!isJudged && timing > 0.15f)
        {
            headJudge = JudgeType.Miss;
            sensor.OnSensorStatusChange -= Check;
            inputManager.OnSensorStatusChange -= Check;
            isJudged = true;
            GameObject.Find("Notes").GetComponent<NoteManager>().noteCount[startPosition]++;
        }

        if(holdTime > 0 && LastFor != 0 ||
           (LastFor == 0 && isJudged))
        {
            userHold.Stop();
            Destroy(tapLine);
            Destroy(holdEffect);
            Destroy(gameObject);
        }

        if(isJudged)
        {
            if (timing < 0.1f || holdTime > -0.2f)
                return;
            if(sensor.Status == SensorStatus.On)
            {
                if(!userHold.IsRunning)
                    userHold.Start();
                PlayHoldEffect();
            }
            else if(sensor.Status == SensorStatus.Off)
            {
                if (userHold.IsRunning)
                    userHold.Stop();
                StopHoldEffect();
            }
        }

    }
    void Check(SensorType s, SensorStatus oStatus, SensorStatus nStatus)
    {
        if (oStatus == SensorStatus.Off && nStatus == SensorStatus.On)
        {
            if (sensor.IsJudging)
                return;
            else
                sensor.IsJudging = true;
            Judge(); 
        }

        if(isJudged)
        {
            sensor.OnSensorStatusChange -= Check;
            inputManager.OnSensorStatusChange -= Check;
        }
    }
    void Judge()
    {

        const int JUDGE_GOOD_AREA = 150;
        const int JUDGE_GREAT_AREA = 100;
        const int JUDGE_PERFECT_AREA = 50;

        const float JUDGE_SEG_PERFECT1 = 16.66667f;
        const float JUDGE_SEG_PERFECT2 = 33.33334f;
        const float JUDGE_SEG_GREAT1 = 66.66667f;
        const float JUDGE_SEG_GREAT2 = 83.33334f;

        if (isJudged)
            return;

        var timing = timeProvider.AudioTime - time;
        var isFast = timing < 0;
        var diff = MathF.Abs(timing * 1000);
        JudgeType result;
        if (diff > JUDGE_GOOD_AREA && isFast)
            return;
        else if (diff < JUDGE_SEG_PERFECT1)
            result = JudgeType.Perfect;
        else if (diff < JUDGE_SEG_PERFECT2)
            result = JudgeType.LatePerfect1;
        else if (diff < JUDGE_PERFECT_AREA)
            result = JudgeType.LatePerfect2;
        else if (diff < JUDGE_SEG_GREAT1)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_SEG_GREAT2)
            result = JudgeType.LateGreat1;
        else if (diff < JUDGE_GREAT_AREA)
            result = JudgeType.LateGreat;
        else if (diff < JUDGE_GOOD_AREA)
            result = JudgeType.LateGood;
        else
            result = JudgeType.Miss;

        if (result != JudgeType.Miss && isFast)
            result = 14 - result;
        if (result != JudgeType.Miss && isEX)
            result = JudgeType.Perfect;

        headJudge = result;
        isJudged = true;
        GameObject.Find("Notes").GetComponent<NoteManager>().noteCount[startPosition]++;
    }
    // Update is called once per frame
    private void Update()
    {
        var timing = timeProvider.AudioTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        if (destScale < 0f)
        {
            destScale = 0f;
            return;
        }

        spriteRenderer.forceRenderingOff = false;
        if (isEX) exSpriteRender.forceRenderingOff = false;

        spriteRenderer.size = new Vector2(1.22f, 1.4f);

        var holdTime = timing - LastFor;
        var holdDistance = holdTime * speed + 4.8f;
        if (holdTime > 0 && GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
        {
            manager.SetSensorOn(sensor.Type, guid);
            if (timing > 0.02)
            {
                Destroy(tapLine);
                Destroy(holdEffect);
                Destroy(gameObject);
            }
            return;
        }

        if (holdTime >= 0)
            return;

        transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));
        tapLine.transform.rotation = transform.rotation;
        holdEffect.transform.position = getPositionFromDistance(4.8f);

        if (isBreak && !holdAnimStart && 
            (GameObject.Find("Input").GetComponent<InputManager>().AutoPlay || sensor.Status == SensorStatus.On))
        {
            var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f, 0);
            spriteRenderer.material.SetFloat("_Brightness", 0.95f + extra);
        }


        if (destScale > 0.3f) tapLine.SetActive(true);

        if (distance < 1.225f)
        {
            transform.localScale = new Vector3(destScale, destScale);
            spriteRenderer.size = new Vector2(1.22f, 1.42f);
            distance = 1.225f;
            var pos = getPositionFromDistance(distance);
            transform.position = pos;            
        }
        else
        {
            if (holdDistance < 1.225f && distance >= 4.8f) // 头到达 尾未出现
            {
                holdDistance = 1.225f;
                distance = 4.8f;
                if (GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
                    PlayHoldEffect();
            }
            else if (holdDistance < 1.225f && distance < 4.8f) // 头未到达 尾未出现
            {
                holdDistance = 1.225f;
            }
            else if (holdDistance >= 1.225f && distance >= 4.8f) // 头到达 尾出现
            {
                distance = 4.8f;
                if (GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
                    PlayHoldEffect();

                holdEndRender.enabled = true;
            }
            else if (holdDistance >= 1.225f && distance < 4.8f) // 头未到达 尾出现
            {
                holdEndRender.enabled = true;
            }

            var dis = (distance - holdDistance) / 2 + holdDistance;
            transform.position = getPositionFromDistance(dis); //0.325
            var size = distance - holdDistance + 1.4f;
            spriteRenderer.size = new Vector2(1.22f, size);
            holdEndRender.transform.localPosition = new Vector3(0f, 0.6825f - size / 2);
            transform.localScale = new Vector3(1f, 1f);
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        lineScale = lineScale >= 1f ? 1f : lineScale;
        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);
        exSpriteRender.size = spriteRenderer.size;
    }
    private void OnDestroy()
    {
        var realityHT = LastFor - 0.3f;
        var percent = MathF.Min(1, (userHold.ElapsedMilliseconds / 1000f) / realityHT);
        JudgeType result = headJudge;
        if(realityHT > 0)
        {
            if (percent == 1)
                result = headJudge;
            else if (percent >= 0.67f)
            {
                if (headJudge == JudgeType.Miss)
                    result = JudgeType.LateGood;
                else if (MathF.Abs((int)headJudge - 7) == 6)
                    result = (int)headJudge < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
                else if (headJudge == JudgeType.Perfect)
                    result = (int)headJudge < 7 ? JudgeType.LatePerfect1 : JudgeType.FastPerfect1;
            }
            else if (percent >= 0.33f)
            {
                if (MathF.Abs((int)headJudge - 7) >= 6)
                    result = (int)headJudge < 7 ? JudgeType.LateGood : JudgeType.FastGood;
                else
                    result = (int)headJudge < 7 ? JudgeType.LateGreat : JudgeType.FastGreat;
            }
            else if (percent >= 0.05f)
                result = (int)headJudge < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            else if (percent >= 0)
            {
                if (headJudge == JudgeType.Miss)
                    result = JudgeType.Miss;
                else
                    result = (int)headJudge < 7 ? JudgeType.LateGood : JudgeType.FastGood;
            }
        }
        var effectManager = GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>();
        effectManager.PlayEffect(startPosition, isBreak, result);
        effectManager.PlayFastLate(startPosition, result);
        if (isBreak)
            GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>().breakCount++;
        else
            GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>().holdCount++;
        if (GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
            manager.SetSensorOff(sensor.Type, guid);
        if(!isJudged)
            GameObject.Find("Notes").GetComponent<NoteManager>().noteCount[startPosition]++;
        sensor.OnSensorStatusChange -= Check;
        inputManager.OnSensorStatusChange -= Check;
    }
    void PlayHoldEffect()
    {
        if(GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
            manager.SetSensorOn(sensor.Type, guid);
        var endTime = time + LastFor;
        GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>().ResetEffect(startPosition);
        holdEffect.SetActive(true);
        

        if (LastFor <= 0.3)
            return;
        else if (!holdAnimStart && timeProvider.AudioTime - time > 0.1)//忽略开头6帧与结尾12帧
        {
            var material = holdEffect.GetComponent<ParticleSystemRenderer>().material;
            switch (headJudge)
            {
                case JudgeType.LateGreat:
                case JudgeType.LateGreat1:
                case JudgeType.LateGreat2:
                case JudgeType.FastGreat2:
                case JudgeType.FastGreat1:
                case JudgeType.FastGreat:
                    material.SetColor("_Color", new Color(1f, 0.44f, 0.70f)); // Pink
                    break;
                case JudgeType.LateGood:
                case JudgeType.FastGood:
                    material.SetColor("_Color", new Color(0.22f, 0.98f, 0.30f)); // Green
                    break;
                case JudgeType.Miss:
                    holdEffect.GetComponent<ParticleSystemRenderer>().material = missMaterial;
                    break;
                default:
                    break;
            }
            holdAnimStart = true;
            animator.runtimeAnimatorController = HoldShine;
            animator.enabled = true;
            var sprRenderer = GetComponent<SpriteRenderer>();
            if (isBreak)
                sprRenderer.sprite = breakHoldOnSpr;
            else if (isEach)
                sprRenderer.sprite = eachHoldOnSpr;
            else
                sprRenderer.sprite = holdOnSpr;
        }
    }
    void StopHoldEffect()
    {
        holdAnimStart = false;
        animator.runtimeAnimatorController = HoldShine;
        animator.enabled = false;
        holdEffect.SetActive(false);
        var sprRenderer = GetComponent<SpriteRenderer>();
        sprRenderer.sprite = holdOffSpr;
    }

    private Vector3 getPositionFromDistance(float distance)
    {
        return new Vector3(
            distance * Mathf.Cos((startPosition * -2f + 5f) * 0.125f * Mathf.PI),
            distance * Mathf.Sin((startPosition * -2f + 5f) * 0.125f * Mathf.PI));
    }
}