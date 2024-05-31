﻿using Assets.Scripts.Interfaces;
using Assets.Scripts.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static NoteEffectManager;

public class SlideDrop : NoteLongDrop, IFlasher
{
    // Start is called before the first frame update
    public GameObject star_slide;

    public Sprite spriteNormal;
    public Sprite spriteEach;
    public Sprite spriteBreak;
    public RuntimeAnimatorController slideShine;
    public RuntimeAnimatorController judgeBreakShine;
    public GameObject parent;

    public bool isMirror;
    public bool isJustR;
    public bool isSpecialFlip; // fixes known star problem
    public bool isEach;
    public bool isBreak;

    // public float time;
    public float timeStart;

    // public float LastFor = 1f;
    public float speed;

    public int startPosition = 1;

    public int sortIndex;

    public float fadeInTime;

    public float fullFadeInTime;

    public float slideConst;
    float arriveTime = -1;

    public List<int> areaStep = new List<int>();
    public bool smoothSlideAnime = false;

    public Material breakMaterial;
    public string slideType;

    public List<Sensor> judgeSensors = new();
    public List<Sensor> triggerSensors = new();
    public List<JudgeArea> judgeQueue = new();
    public List<JudgeArea> _judgeQueue = new();
    public List<GameObject> SubSlides = new();

    public ConnSlideInfo ConnectInfo { get; set; }
    public bool isFinished { get => judgeQueue.Count == 0; }
    bool canShine = false;

    Animator fadeInAnimator = null;

    private readonly List<Animator> animators = new();

    private readonly List<GameObject> slideBars = new();

    private readonly List<Vector3> slidePositions = new();
    private readonly List<Quaternion> slideRotations = new();
    private GameObject slideOK;

    private SpriteRenderer spriteRenderer_star;

    private bool startShining;
    bool isDestroying = false;

    private AudioTimeProvider timeProvider;

    public int endPosition;

    Guid guid = Guid.NewGuid();
    List<GameObject> sensors = new();
    SensorManager sManager;

    bool isJudged = false;
    bool canCheck = false;
    List<Sensor> registerSensors = new();

    private void Start()
    {
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();        
        // 计算Slide淡入时机
        // 在8.0速时应当提前300ms显示Slide
        fadeInTime = -3.926913f / speed ;
        // Slide完全淡入时机
        // 正常情况下应为负值；速度过高将忽略淡入
        fullFadeInTime = Math.Min(fadeInTime + 0.2f,0);
        var interval = fullFadeInTime - fadeInTime;
        fadeInAnimator = this.GetComponent<Animator>();
        //淡入时机与正解帧间隔小于200ms时，加快淡入动画的播放速度; interval永不为0
        fadeInAnimator.speed = 0.2f / interval; 
        fadeInAnimator.SetTrigger("slide");

        // Connection Slide Handle



        if(SubSlides.Count != 0)
        {
            foreach(var obj in SubSlides)
            {
                var slide = obj.GetComponent<SlideDrop>();
                var slideBars = slide.GetSlideBars();
                foreach (var bar in slideBars)
                    bar.transform.SetParent(transform);
                Destroy(obj);
            }
            
        }


        
        var sManagerObj = GameObject.Find("Sensors");
        var count = sManagerObj.transform.childCount;
        for (int i = 0; i < count;i++)
            sensors.Add(sManagerObj.transform.GetChild(i).gameObject);
        sManager = sManagerObj.GetComponent<SensorManager>();

        GetSensors(sensors.Select(x => x.GetComponent<RectTransform>())
                                        .ToArray());

        var _slideIndex = areaStep.Skip(1).ToArray();
        if (_slideIndex.Length != judgeSensors.Count)
            _slideIndex = null;
        for(int i =0; i < judgeSensors.Count;i++)
        {
            var sensor = judgeSensors[i];
            int index = 0;
            if (_slideIndex is null)
                index = (slideBars.Count / judgeSensors.Count) * (i + 1);
            else
                index = _slideIndex[i];
            judgeQueue.Add(new JudgeArea(
                new Dictionary<Sensor.SensorType, bool> 
                { 
                    {sensor.Type, i == judgeSensors.Count - 1 } 
                }, index));
        }
        if(slideType is "line3" or "line7")// 1-3
        {
            judgeQueue[1].CanSkip = false;
            judgeQueue[1].AddArea(judgeSensors[1].Type + 8);
            registerSensors.Add(sManager.GetSensor(judgeSensors[1].Type + 8));
        }
        else if(slideType == "circle3")// 1^3
            judgeQueue[1].CanSkip = false;
        else if (slideType[0] == 'L')// 1^3
        {
            judgeQueue[1].CanSkip = false;
            judgeQueue[1].AddArea(judgeSensors[1].Type + 8);
            registerSensors.Add(sManager.GetSensor(judgeSensors[1].Type + 8));
            if (slideType == "L5")
            { 
                judgeQueue[3].CanSkip = false;
                judgeQueue[3].AddArea(judgeSensors[3].Type + 8);
                registerSensors.Add(sManager.GetSensor(judgeSensors[3].Type + 8));
            }
        }
        judgeQueue.LastOrDefault().SetIsLast();
        registerSensors.AddRange(judgeSensors);
        _judgeQueue = new(judgeQueue);

        parent = ConnectInfo.Parent;
    }

    void GetSensors(RectTransform[] sensors)
    {
        Sensor lastSensor = null;
        foreach (var bar in slideBars)
        {
            var pos = bar.transform.position;
            
            foreach (var s in sensors)
            {
                var sensor = s.GetComponent<Sensor>();
                if (sensor.Group == Sensor.SensorGroup.E || sensor.Group == Sensor.SensorGroup.D)
                    continue;

                var rCenter = s.position;
                var rWidth = s.rect.width * s.lossyScale.x;
                var rHeight = s.rect.height * s.lossyScale.y;

                var radius = Math.Max(rWidth, rHeight) / 2;

                if ((pos - rCenter).sqrMagnitude <= radius * radius)
                {
                    if(lastSensor is null || sensor != lastSensor)
                    {
                        judgeSensors.Add(sensor);
                        lastSensor = sensor;
                        break;
                    }
                }
            }
        }
        
    }
    private void FixedUpdate()
    {
        /// time      是Slide启动的时间点
        /// timeStart 是Slide完全显示但未启动
        /// LastFor   是Slide的时值
        var timing = timeProvider.AudioTime - time;
        var startTiming = timeProvider.AudioTime - timeStart;
        var forceJudgeTiming = time + LastFor + 0.6;

        if (ConnectInfo.IsGroupPart)
        {
            if (ConnectInfo.IsGroupPartHead && startTiming >= -0.040f)
                canCheck = true;
            else if (!ConnectInfo.IsGroupPartHead)
                canCheck = ConnectInfo.ParentFinished;
        }
        else if (startTiming >= -0.050f)
            canCheck = true;

        if (timing > 0)
            Running();

        if (ConnectInfo.IsConnSlide)
        {
            if(ConnectInfo.IsGroupPartEnd && isFinished)
            {
                HideBar(areaStep.LastOrDefault());
                Judge();
            }
            else if (ConnectInfo.IsGroupPartEnd && timeProvider.AudioTime - forceJudgeTiming >= 0)
                TooLateJudge();
            else if(isFinished)
                HideBar(areaStep.LastOrDefault());
        }
        else if (isFinished)
        {
            HideBar(areaStep.LastOrDefault());
            Judge();
        }
        else if (timeProvider.AudioTime - forceJudgeTiming >= 0)
            TooLateJudge();
    }
    // Update is called once per frame
    private void Update()
    {
        Check();
        if (star_slide == null)
        {
            if (isFinished)
                DestroySelf();
            return;
        }
        // Slide淡入期间，不透明度从0到0.55耗时200ms
        var startiming = timeProvider.AudioTime - timeStart;
        if (startiming <= 0f)
        {
            if (!fadeInAnimator.enabled && startiming >= fadeInTime)
                fadeInAnimator.enabled = true;
            return;
        }
        fadeInAnimator.enabled = false;
        setSlideBarAlpha(1f);

        star_slide.SetActive(true);
        var timing = timeProvider.AudioTime - time;
        if (timing <= 0f)
        {
            canShine = true;
            float alpha;
            if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartHead)
                alpha = 0;
            else
            {
                // 只有当它是一个起点Slide（而非Slide Group中的子部分）的时候，才会有开始的星星渐入动画
                alpha = 1f - -timing / (time - timeStart);
                alpha = alpha > 1f ? 1f : alpha;
                alpha = alpha < 0f ? 0f : alpha;                
            }

            spriteRenderer_star.color = new Color(1, 1, 1, alpha);
            star_slide.transform.localScale = new Vector3(alpha + 0.5f, alpha + 0.5f, alpha + 0.5f);
            star_slide.transform.position = slidePositions[0];
            applyStarRotation(slideRotations[0]);
        }
        else
        {
            var process = (LastFor - timing) / LastFor;
            process = MathF.Min(1f - process, 1f);
            var pos = (slidePositions.Count - 1) * process;
            var index = Math.Min((int)pos, slidePositions.Count);
            try
            {
                if (index == slidePositions.Count - 1)
                {
                    spriteRenderer_star.color = Color.white;
                    star_slide.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

                    star_slide.transform.position = slidePositions[index];
                    
                    if (ConnectInfo.IsConnSlide && !ConnectInfo.IsGroupPartEnd)
                        DestroySelf(true);
                    else if (isFinished && isJudged)
                        DestroySelf();
                }
                else
                {
                    spriteRenderer_star.color = Color.white;
                    star_slide.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

                    try
                    {
                        star_slide.transform.position = (slidePositions[index + 1] - slidePositions[index]) * (pos - index) +
                                                        slidePositions[index];
                        var delta = Mathf.DeltaAngle(slideRotations[index + 1].eulerAngles.z,
                            slideRotations[index].eulerAngles.z) * (pos - index);
                        delta = Mathf.Abs(delta);
                        applyStarRotation(
                            Quaternion.Euler(0f, 0f,
                                Mathf.MoveTowardsAngle(slideRotations[index].eulerAngles.z,
                                    slideRotations[index + 1].eulerAngles.z, delta)
                            )
                        );
                    }
                    catch
                    {
                    }
                }
                Running();
            }
            catch (Exception e)
            {
            }
        }

        Check();
    }
    public void Check()
    {
        if (isFinished || !canCheck)
            return;
        try
        {
            if (judgeQueue.Count == 0)
                return;

            var first = judgeQueue.First();
            JudgeArea second = null;

            if (judgeQueue.Count >= 2)
                second = judgeQueue[1];
            var fType = first.GetSensorTypes();
            foreach (var t in fType)
            {
                var sensor = sManager.GetSensor(t);
                first.Judge(t, sensor.Status);
            }

            if (second is not null && (first.CanSkip || first.On))
            {
                var sType = second.GetSensorTypes();
                foreach (var t in sType)
                {
                    var sensor = sManager.GetSensor(t);
                    second.Judge(t, sensor.Status);
                }

                if (second.IsFinished)
                {
                    HideBar(first.SlideIndex);
                    judgeQueue = judgeQueue.Skip(2).ToList();
                    return;
                }
                else if (second.On)
                {
                    HideBar(first.SlideIndex);
                    judgeQueue = judgeQueue.Skip(1).ToList();
                    return;
                }
            }

            if (first.IsFinished)
            {
                HideBar(first.SlideIndex);
                judgeQueue = judgeQueue.Skip(1).ToList();
                return;
            }
        }
        catch(Exception e)
        {
            print(e);
        }
    }
    void HideBar(int endIndex)
    {
        endIndex = Math.Min(endIndex, slideBars.Count - 1);
        for (int i = 0; i <= endIndex; i++)
            slideBars[i].SetActive(false);
    }
    void Running()
    {
        if (star_slide == null || !GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
            return;

        var starRadius = 0.763736616f;
        var starPos = star_slide.transform.position;
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
    void Judge()
    {
        if (!ConnectInfo.IsGroupPartEnd)
            return;
        var timing = timeProvider.AudioTime - time;
        var starTiming = timeStart + (time - timeStart) * 0.667;
        var pTime = LastFor / areaStep.Last();
        var judgeTime = time + pTime * (areaStep.LastOrDefault() - 3.5f);// 正解帧
        var stayTime = (time + LastFor) - judgeTime; // 停留时间
        if (!isJudged)
        {
            arriveTime = timeProvider.AudioTime;
            var triggerTime = timeProvider.AudioTime;           

            const float totalInterval = 1.2f; // 秒
            const float nPInterval = 0.4666667f; // Perfect基础区间

            float extInterval = MathF.Min(stayTime / 4, 0.733333f);           // Perfect额外区间
            float pInterval = MathF.Min(nPInterval + extInterval, totalInterval);// Perfect总区间
            var ext = MathF.Max(extInterval - 0.4f,0);
            float grInterval = MathF.Max(0.4f - extInterval, 0);        // Great总区间
            float gdInterval = MathF.Max(0.3333334f - ext, 0); // Good总区间

            var diff = judgeTime - triggerTime; // 大于0为Fast，小于为Late
            bool isFast = false;
            JudgeType? judge = null;

            if (diff > 0)
                isFast = true;

            var p = pInterval / 2;
            var gr = grInterval / 2;
            var gd = gdInterval / 2;
            diff = MathF.Abs(diff);

            if( gr == 0 )
            {
                if(diff >= p)
                    judge = isFast ? JudgeType.FastGood : JudgeType.LateGood;
                else
                    judge = JudgeType.Perfect;
            }
            else
            {
                if (diff >= gr + p || diff >= totalInterval / 2)
                    judge = isFast ? JudgeType.FastGood : JudgeType.LateGood;
                else if (diff >= p)
                    judge = isFast ? JudgeType.FastGreat : JudgeType.LateGreat;
                else
                    judge = JudgeType.Perfect;
            }            

            switch (judge)
            {
                case JudgeType.FastGreat:
                    slideOK.GetComponent<LoadJustSprite>().setFastGr();
                    break;
                case JudgeType.FastGood:
                    slideOK.GetComponent<LoadJustSprite>().setFastGd();
                    break;
                case JudgeType.LateGood:
                    slideOK.GetComponent<LoadJustSprite>().setLateGd();
                    break;
                case JudgeType.LateGreat:
                    slideOK.GetComponent<LoadJustSprite>().setLateGr();
                    break;
                    
            }
            print($"diff : {MathF.Round(diff * 1000,2)} ms");
            isJudged = true;
        }
        else if (arriveTime < starTiming && timeProvider.AudioTime >= starTiming + stayTime * 0.667)
            DestroySelf();
        else if (arriveTime >= starTiming && timeProvider.AudioTime >= arriveTime + stayTime * 0.667)
            DestroySelf();
    }
    void TooLateJudge()
    {
        if (judgeQueue.Count == 1)
            slideOK.GetComponent<LoadJustSprite>().setLateGd();
        else
            slideOK.GetComponent<LoadJustSprite>().setMiss();
        DestroySelf();
    }
    void DestroySelf(bool onlyStar = false)
    {
        if (onlyStar)
            Destroy(star_slide);
        else
        {
            if(ConnectInfo.Parent != null)
                Destroy(ConnectInfo.Parent);

            foreach (GameObject obj in slideBars)
                obj.SetActive(false);

            if (star_slide != null)
                Destroy(star_slide);
            Destroy(gameObject);
        }
    }
    void OnDestroy()
    {
        if (isDestroying)
            return;
        var sensors = _judgeQueue.SelectMany(x => x.GetAreas())
                           .Select(x => x.Type);
        if (ConnectInfo.Parent != null)
            Destroy(ConnectInfo.Parent);
        if(star_slide != null)
            Destroy(star_slide);
        if (ConnectInfo.IsGroupPartEnd)
        {
            // 只有组内最后一个Slide完成 才会显示判定条并增加总数
            if (isBreak)
                GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>().breakCount++;
            else
                GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>().slideCount++;
            slideOK.SetActive(true);
        }
        else
        {
            // 如果不是组内最后一个 那么也要将判定条删掉
            Destroy(slideOK);
        }
        foreach (var t in sensors)
            sManager.SetSensorOff(t, guid);
        isDestroying = true;
    }
    
    private void OnEnable()
    {
        slideOK = transform.GetChild(transform.childCount - 1).gameObject; //slideok is the last one        

        if(isBreak)
            slideOK.GetComponent<Animator>().runtimeAnimatorController = judgeBreakShine;

        if (isMirror)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, -45f * startPosition);
            slideOK.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, -45f * (startPosition - 1));
        }

        if (isJustR)
        {
            if (slideOK.GetComponent<LoadJustSprite>().setR() == 1 && isMirror)
            {
                slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                var angel = slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
            }
        }
        else
        {
            if (slideOK.GetComponent<LoadJustSprite>().setL() == 1 && !isMirror)
            {
                slideOK.transform.Rotate(new Vector3(0f, 0f, 180f));
                var angel = slideOK.transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
                slideOK.transform.position += new Vector3(Mathf.Sin(angel) * 0.27f, Mathf.Cos(angel) * -0.27f);
            }
        }


        spriteRenderer_star = star_slide.GetComponent<SpriteRenderer>();

        if(isBreak)
        {
            spriteRenderer_star.material = breakMaterial;
            spriteRenderer_star.material.SetFloat("_Brightness", 0.95f);
            var controller = star_slide.AddComponent<BreakShineController>();
            controller.enabled = true;
            controller.parent = this;
        }

        for (var i = 0; i < transform.childCount - 1; i++) slideBars.Add(transform.GetChild(i).gameObject);

        slideOK.SetActive(false);
        slideOK.transform.SetParent(transform.parent);
        slidePositions.Add(getPositionFromDistance(4.8f));
        foreach (var bars in slideBars)
        {
            slidePositions.Add(bars.transform.position);
            slideRotations.Add(Quaternion.Euler(bars.transform.rotation.eulerAngles + new Vector3(0f, 0f, 18f)));
        }
        
        foreach (var gm in slideBars)
        {
            var sr = gm.GetComponent<SpriteRenderer>();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = sortIndex--;
            sr.sortingLayerName = "Slide";
            if (isBreak)
            {                
                sr.sprite = spriteBreak;
                sr.material = breakMaterial;
                sr.material.SetFloat("_Brightness", 0.95f);
                var controller = gm.AddComponent<BreakShineController>();
                controller.parent = this;
                controller.enabled = true;
                //anim.runtimeAnimatorController = slideShine;
                //anim.enabled = false;
                //animators.Add(anim);
            }
            else if (isEach)
            {
                sr.sprite = spriteEach;
            }
            else
            {
                sr.sprite = spriteNormal;
            }
        }
    }
    private void setSlideBarAlpha(float alpha)
    {
        foreach (var gm in slideBars) gm.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, alpha);
    }

    private Vector3 getPositionFromDistance(float distance)
    {
        return new Vector3(
            distance * Mathf.Cos((startPosition * -2f + 5f) * 0.125f * Mathf.PI),
            distance * Mathf.Sin((startPosition * -2f + 5f) * 0.125f * Mathf.PI));
    }

    private void applyStarRotation(Quaternion newRotation)
    {
        var halfFlip = newRotation.eulerAngles;
        halfFlip.z += 180f;
        if (isSpecialFlip)
            star_slide.transform.rotation = Quaternion.Euler(halfFlip);
        else
            star_slide.transform.rotation = newRotation;
    }
    public GameObject[] GetSlideBars() => slideBars.ToArray();
    public bool CanShine() => canShine;
}