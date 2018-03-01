using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel;
using Unity.Collections;

namespace MainContents
{
    public sealed class DokabenJobSystem : MonoBehaviour
    {
        // ------------------------------
        #region // Constants

        // コマ数
        const int Framerate = 9;

        // 1コマに於ける回転角度
        const float Angle = (90f / Framerate); // 90度をフレームレートで分割

        // コマ中の待機時間
        const float Interval = 0.2f;

        #endregion // Constants

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
            // 算出した回転情報を保持
            public Matrix4x4 Matrix;

            public DokabenStruct(float currentAngle)
            {
                this.CurrentAngle = currentAngle;
                this.DeltaTimeCounter = 0f;
                this.FrameCounter = 0;
                this.Matrix = new Matrix4x4();
            }
        }

        /// <summary>
        /// 回転計算用のJob
        /// </summary>
        struct MyParallelForUpdate : IJobParallelFor
        {
            public NativeArray<DokabenStruct> Accessor;
            public float DeltaTime;

            // Jobで実行されるコード実行
            public void Execute(int index)
            {
                DokabenStruct accessor = this.Accessor[index];
                Matrix4x4 m = Matrix4x4.identity;
                float x = 0f, y = 0f, z = 0f;
                m.SetTRS(new Vector3(x, y, z), Quaternion.identity, Vector3.one);
                if (accessor.DeltaTimeCounter >= Interval)
                {
                    // 原点を-0.5ずらして下端に設定
                    float halfY = y - 0.5f;
                    float rot = accessor.CurrentAngle * Mathf.Deg2Rad;
                    float sin = Mathf.Sin(rot);
                    float cos = Mathf.Cos(rot);
                    // 任意の原点周りにX軸回転を行う
                    m.m11 = cos;
                    m.m12 = -sin;
                    m.m21 = sin;
                    m.m22 = cos;
                    m.m13 = halfY - halfY * cos + z * sin;
                    m.m23 = z - halfY * sin - z * cos;

                    accessor.FrameCounter = accessor.FrameCounter + 1;
                    if (accessor.FrameCounter >= Framerate)
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
                accessor.Matrix = m;
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

        // 実行粒度
        [SerializeField] int _innerloopBatchCount = 7;

        #endregion // Private Members(Editable)

        // ------------------------------
        #region // Private Members

        // 生成したドカベンのMeshFilter
        MeshFilter[] _dokabens = null;

        // Jobの終了待ち等を行うHandle
        JobHandle _jobHandle;

        // Job用の回転計算用データ
        NativeArray<DokabenStruct> _dokabenStructs;

        #endregion  // Private Members


        // ------------------------------
        #region // Unity Events

        void Start()
        {
            // 回転計算用データのメモリ確保
            this._dokabenStructs = new NativeArray<DokabenStruct>(this._maxObjectNum, Allocator.Persistent);
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                this._dokabenStructs[i] = new DokabenStruct(Angle);
            }

            // ドカベンの生成
            this._dokabens = new MeshFilter[this._maxObjectNum];
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var obj = Instantiate<GameObject>(this._dokabenPrefab);
                this._dokabens[i] = obj.GetComponent<MeshFilter>();
                obj.transform.position = new Vector3(
                    (i % this._cellNum) * (this._positionInterval.x),
                    ((i / this._cellNum) % this._cellNum) * this._positionInterval.y,
                    (i / (this._cellNum * this._cellNum)) * this._positionInterval.z);
            }
        }

        void Update()
        {
            // 回転計算用jobの作成
            MyParallelForUpdate rotateJob = new MyParallelForUpdate()
            {
                Accessor = this._dokabenStructs,
                DeltaTime = Time.deltaTime,
            };
            // Jobの実行
            this._jobHandle = rotateJob.Schedule(this._maxObjectNum, this._innerloopBatchCount);
            // Jobが終わるまで処理をブロック
            this._jobHandle.Complete();

            // 計算結果を各ドカベンに反映
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var meshFilter = this._dokabens[i];
                var matrix = this._dokabenStructs[i].Matrix;
                var origVerts = meshFilter.mesh.vertices;
                var newVerts = new Vector3[origVerts.Length];
                for (int j = 0; j < origVerts.Length; ++j)
                {
                    newVerts[j] = matrix.MultiplyPoint3x4(origVerts[j]);
                }
                meshFilter.mesh.vertices = newVerts;
            }
        }

        void OnDestroy()
        {
            // 解放/破棄など
            this._jobHandle.Complete();
            this._dokabenStructs.Dispose();
        }

        #endregion  // Unity Events
    }
}


// クラスRotate
// https://docs.oracle.com/javase/jp/8/javafx/api/javafx/scene/transform/Rotate.html

// 任意点周りの回転移動
// http://imagingsolution.blog107.fc2.com/blog-entry-111.html