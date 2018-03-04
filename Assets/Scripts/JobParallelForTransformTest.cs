using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel;
using Unity.Collections;

namespace MainContents
{
    public class JobParallelForTransformTest : MonoBehaviour
    {
        // ------------------------------
        #region // Defines

        /// <summary>
        /// 回転計算用データ
        /// </summary>
        struct DokabenStruct
        {
            // 経過時間計測用
            public float DeltaTimeCounter;
            // コマ数のカウンタ
            public int FrameCounter;
            // 1コマに於ける回転角度
            public float CurrentAngle;
            // 現在の回転角度
            public float CurrentRot;

            public DokabenStruct(float currentAngle)
            {
                this.CurrentAngle = currentAngle;
                this.DeltaTimeCounter = 0f;
                this.FrameCounter = 0;
                this.CurrentRot = 0f;
            }
        }

        /// <summary>
        /// 回転計算用のJob(Transformに直接アクセスして計算)
        /// </summary>
        struct MyParallelForTransformUpdate : IJobParallelForTransform
        {
            public NativeArray<DokabenStruct> Accessor;
            public float DeltaTime;

            public void Execute(int index, TransformAccess transform)
            {
                DokabenStruct accessor = this.Accessor[index];
                if (accessor.DeltaTimeCounter >= Constants.Interval)
                {
                    accessor.CurrentRot += accessor.CurrentAngle;
                    transform.rotation = Quaternion.AngleAxis(accessor.CurrentRot, -Vector3.right);
                    accessor.FrameCounter = accessor.FrameCounter + 1;
                    if (accessor.FrameCounter >= Constants.Framerate)
                    {
                        accessor.CurrentAngle = -accessor.CurrentAngle;
                        accessor.FrameCounter = 0;
                    }
                    accessor.DeltaTimeCounter = 0f;
                }
                else
                {
                    accessor.DeltaTimeCounter += this.DeltaTime;
                }
                this.Accessor[index] = accessor;
            }
        }

        #endregion // Defines

        // ------------------------------
        #region // Private Members(Editable)

        // ドカベンのPrefab
        [SerializeField] GameObject _dokabenPrefab;

        // 最大オブジェクト数
        [SerializeField] int _maxObjectNum;

        // 配置数
        [SerializeField] int _cellNum;

        // 配置間隔
        [SerializeField] Vector3 _positionInterval;

        #endregion // Private Members(Editable)

        // ------------------------------
        #region // Private Members

        // Jobの終了待ち等を行うHandle
        JobHandle _jobHandle;

        // Job用の回転計算用データ
        NativeArray<DokabenStruct> _dokabenStructs;

        // MyParallelForTransformUpdate実行用配列
        TransformAccessArray _dokabenTransformAccessArray;

        #endregion  // Private Members


        // ----------------------------------------------------
        #region // Unity Events

        void Start()
        {
            // 回転計算用データのメモリ確保
            this._dokabenStructs = new NativeArray<DokabenStruct>(this._maxObjectNum, Allocator.Persistent);
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                this._dokabenStructs[i] = new DokabenStruct(Constants.Angle);
            }

            // ドカベンの生成
            this._dokabenTransformAccessArray = new TransformAccessArray(this._maxObjectNum);
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var obj = Instantiate<GameObject>(this._dokabenPrefab);
                var trs = obj.transform;
                trs.transform.position = new Vector3(
                    (i % this._cellNum) * (this._positionInterval.x),
                    ((i / this._cellNum) % this._cellNum) * this._positionInterval.y,
                    (i / (this._cellNum * this._cellNum)) * this._positionInterval.z);
                this._dokabenTransformAccessArray.Add(trs);
            }
        }

        void Update()
        {
            this._jobHandle.Complete();
            MyParallelForTransformUpdate rotateJob = new MyParallelForTransformUpdate()
            {
                Accessor = this._dokabenStructs,
                DeltaTime = Time.deltaTime,
            };
            this._jobHandle = rotateJob.Schedule(this._dokabenTransformAccessArray);
            JobHandle.ScheduleBatchedJobs();
        }

        void OnDestroy()
        {
            // 解放/破棄など
            this._jobHandle.Complete();
            this._dokabenStructs.Dispose();
            this._dokabenTransformAccessArray.Dispose();
        }

        #endregion  // Unity Events
    }
}