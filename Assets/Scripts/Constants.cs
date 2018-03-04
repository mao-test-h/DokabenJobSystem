using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainContents
{
    // 共通定数
    public class Constants
    {
        // コマ数
        public const int Framerate = 9;
        // 1コマに於ける回転角度
        public const float Angle = (90f / Framerate); // 90度をコマ数で分割
        // コマ中の待機時間
        public const float Interval = 0.2f;
    }
}