﻿using UnityEngine;

namespace Assets.Scripts.Notes
{
    public class ConnSlideInfo
    {
        public bool IsGroupPartHead { get; set; }
        public bool IsGroupPart { get; set; }
        public bool IsGroupPartEnd { get; set; }
        public GameObject Parent { get; set; } = null;
        public bool DestroyAfterJudge 
        {
            get => IsGroupPartEnd;
        }
        public bool IsConnSlide { get => IsGroupPart; }
        public bool ParentFinished 
        {
            get
            {
                if (Parent == null)
                    return true;
                else 
                    return Parent.GetComponent<SlideDrop>().isFinished;
            }
        }


    }
}
