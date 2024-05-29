using System;
using Unity.Mathematics;
using UnityEngine;
using static NoteEffectManager;
using static Sensor;
using Random = System.Random;

public class StarDrop : NoteDrop
{
    // Start is called before the first frame update
    // public float time;
    public int startPosition = 1;
    public float speed = 1;
    public float rotateSpeed = 1f;

    public bool isEach;
    public bool isBreak;
    public bool isDouble;
    public bool isEX;
    public bool isNoHead;

    public Sprite tapSpr;
    public Sprite eachSpr;
    public Sprite breakSpr;
    public Sprite exSpr;

    public Sprite tapSpr_Double;
    public Sprite eachSpr_Double;
    public Sprite breakSpr_Double;
    public Sprite exSpr_Double;

    public Sprite eachLine;
    public Sprite breakLine;

    public RuntimeAnimatorController BreakShine;

    public GameObject slide;
    public GameObject tapLine;

    public Color exEffectTap;
    public Color exEffectEach;
    public Color exEffectBreak;
    private Animator animator;

    public Material breakMaterial;

    private bool breakAnimStart;
    private SpriteRenderer exSpriteRender;
    private SpriteRenderer lineSpriteRender;

    private ObjectCounter ObjectCounter;

    private SpriteRenderer spriteRenderer;

    private AudioTimeProvider timeProvider;

    public Guid guid = Guid.NewGuid();
    SensorManager manager;
    Sensor sensor;
    NoteManager noteManager;
    JudgeType judgeResult;
    bool isJudged = false;

    private void Start()
    {
        var notes = GameObject.Find("Notes").transform;
        noteManager = notes.GetComponent<NoteManager>();
        tapLine = Instantiate(tapLine, notes);
        tapLine.SetActive(false);
        lineSpriteRender = tapLine.GetComponent<SpriteRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        exSpriteRender = transform.GetChild(0).GetComponent<SpriteRenderer>();
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        ObjectCounter = GameObject.Find("ObjectCounter").GetComponent<ObjectCounter>();

        spriteRenderer.sortingOrder += noteSortOrder;
        exSpriteRender.sortingOrder += noteSortOrder;

        if (isDouble)
        {
            exSpriteRender.sprite = exSpr_Double;
            spriteRenderer.sprite = tapSpr_Double;
            if (isEX) exSpriteRender.color = exEffectTap;
            if (isEach)
            {
                lineSpriteRender.sprite = eachLine;
                spriteRenderer.sprite = eachSpr_Double;
                if (isEX) exSpriteRender.color = exEffectEach;
            }

            if (isBreak)
            {
                lineSpriteRender.sprite = breakLine;
                spriteRenderer.sprite = breakSpr_Double;
                if (isEX) exSpriteRender.color = exEffectBreak;
                spriteRenderer.material = breakMaterial;
            }
        }
        else
        {
            exSpriteRender.sprite = exSpr;
            spriteRenderer.sprite = tapSpr;
            if (isEX) exSpriteRender.color = exEffectTap;
            if (isEach)
            {
                lineSpriteRender.sprite = eachLine;
                spriteRenderer.sprite = eachSpr;
                if (isEX) exSpriteRender.color = exEffectEach;
            }

            if (isBreak)
            {
                lineSpriteRender.sprite = breakLine;
                spriteRenderer.sprite = breakSpr;
                if (isEX) exSpriteRender.color = exEffectBreak;
                spriteRenderer.material = breakMaterial;
            }
        }

        spriteRenderer.forceRenderingOff = true;
        exSpriteRender.forceRenderingOff = true;

        if(!isNoHead)
        {
            sensor = GameObject.Find("Sensors")
                                   .transform.GetChild(startPosition - 1)
                                   .GetComponent<Sensor>();
            manager = GameObject.Find("Sensors")
                                    .GetComponent<SensorManager>();
            sensor.OnSensorStatusChange += Check;
        }
    }
    private void FixedUpdate()
    {
        if (!isJudged && timeProvider.AudioTime - time > 0.15f)
        {
            judgeResult = JudgeType.Miss;
            Destroy(tapLine);
            Destroy(gameObject);
        }
        else if (isJudged)
        {
            Destroy(tapLine);
            Destroy(gameObject);
        }
    }
    // Update is called once per frame
    private void Update()
    {
        var timing = timeProvider.AudioTime - time;
        var distance = timing * speed + 4.8f;
        var destScale = distance * 0.4f + 0.51f;
        var songSpeed = timeProvider.CurrentSpeed;
        if (destScale < 0f)
        {
            destScale = 0f;
            return;
        }

        if (!isNoHead)
        {
            spriteRenderer.forceRenderingOff = false;
            if (isEX) exSpriteRender.forceRenderingOff = false;
        }

        if(isBreak)
        {
            var extra = Math.Max(Mathf.Sin(timeProvider.GetFrame() * 0.17f) * 0.5f,0);
            spriteRenderer.material.SetFloat("_Brightness",0.95f + extra);
        }

        

        if (timeProvider.isStart)
            transform.Rotate(0f, 0f, -180f * Time.deltaTime * songSpeed / rotateSpeed);

        tapLine.transform.rotation = Quaternion.Euler(0, 0, -22.5f + -45f * (startPosition - 1));

        if (destScale > 0.3f && !isNoHead) tapLine.SetActive(true);

        if (distance < 1.225f)
        {
            transform.localScale = new Vector3(destScale, destScale);

            distance = 1.225f;
            var pos = getPositionFromDistance(distance);
            transform.position = pos;
        }
        else
        {
            if (!slide.activeSelf) slide.SetActive(true);
            var pos = getPositionFromDistance(distance);
            transform.position = pos;
            transform.localScale = new Vector3(1f, 1f);
        }

        var lineScale = Mathf.Abs(distance / 4.8f);
        tapLine.transform.localScale = new Vector3(lineScale, lineScale, 1f);

        if(timing > 0 && isNoHead)
        {
            Destroy(tapLine);
            Destroy(gameObject);
        }
        else if (timing > 0 && GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
        {
            manager.SetSensorOn(sensor.Type, guid);

            if (timing > 0.02)
            {
                judgeResult = JudgeType.Perfect;
                Destroy(tapLine);
                Destroy(gameObject);
            }
        }
    }
    void Check(SensorType s, SensorStatus oStatus, SensorStatus nStatus)
    {
        if (!noteManager.CanJudge(gameObject, startPosition))
            return;
        else if (oStatus == SensorStatus.Off && nStatus == SensorStatus.On)
            Judge();
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

        judgeResult = result;
        isJudged = true;
    }
    private void OnDestroy()
    {
        if(!isNoHead)
        {
            GameObject.Find("NoteEffects").GetComponent<NoteEffectManager>().PlayEffect(startPosition, isBreak,judgeResult);
            if (isBreak) ObjectCounter.breakCount++;
            else ObjectCounter.tapCount++;
            GameObject.Find("Notes").GetComponent<NoteManager>().noteCount[startPosition]++;
            if (GameObject.Find("Input").GetComponent<InputManager>().AutoPlay)
                manager.SetSensorOff(sensor.Type, guid);
            sensor.OnSensorStatusChange -= Check;
        }
    }
    private Vector3 getPositionFromDistance(float distance)
    {
        return new Vector3(
            distance * Mathf.Cos((startPosition * -2f + 5f) * 0.125f * Mathf.PI),
            distance * Mathf.Sin((startPosition * -2f + 5f) * 0.125f * Mathf.PI));
    }
}