using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Jobs;
using Unity.Jobs;
using Unity.Jobs.LowLevel;
using Unity.Collections;

namespace MainContents
{
    public sealed class JobParallelForTest : MonoBehaviour
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
                this.Accessor[index] = JobParallelForTest.Rotate(this.DeltaTime, accessor);
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

        // JobSystemでの実行ならtrue
        [SerializeField] bool _jobSystem = false;

        #endregion // Private Members(Editable)

        // ------------------------------
        #region // Private Members

        // 生成したドカベンのMeshFilter
        MeshFilter[] _dokabens = null;

        // Jobの終了待ち等を行うHandle
        JobHandle _jobHandle;

        // Job用の回転計算用データ
        NativeArray<DokabenStruct> _dokabenStructs;

        // mesh頂点の数
        int _vertsLen = 0;

        // メッシュの頂点のバッファ
        Vector3[] _vertsBuff = null;

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
            this._dokabens = new MeshFilter[this._maxObjectNum];
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var obj = Instantiate<GameObject>(this._dokabenPrefab);
                this._dokabens[i] = obj.GetComponent<MeshFilter>();
                obj.transform.position = new Vector3(
                    (i % this._cellNum) * (this._positionInterval.x),
                    ((i / this._cellNum) % this._cellNum) * this._positionInterval.y,
                    (i / (this._cellNum * this._cellNum)) * this._positionInterval.z);
                if (i > this._maxObjectNum - 2)
                {
                    this._vertsLen = this._dokabens[i].mesh.vertices.Length;
                }
            }
            this._vertsBuff = new Vector3[this._vertsLen];
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (this._jobSystem)
            {
                this.JobSystemCalculation(deltaTime);
            }
            else
            {
                this.UpdateCalculation(deltaTime);
            }

            // 計算結果を各ドカベンに反映
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var matrix = this._dokabenStructs[i];
                var mesh = this._dokabens[i].mesh;
                var origVerts = mesh.vertices;
                for (int j = 0; j < this._vertsLen; ++j)
                {
                    this._vertsBuff[j] = matrix.Matrix.MultiplyPoint3x4(origVerts[j]);
                }
                mesh.vertices = this._vertsBuff;
            }
        }

        void OnDestroy()
        {
            // 解放/破棄など
            this._jobHandle.Complete();
            this._dokabenStructs.Dispose();
        }

        #endregion  // Unity Events

        // ----------------------------------------------------
        #region // Private Methods

        // 配列を回して計算するテスト(MainThread上で実行)
        void UpdateCalculation(float deltaTime)
        {
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                this._dokabenStructs[i] = JobParallelForTest.Rotate(deltaTime, this._dokabenStructs[i]);
            }
        }

        // JobSystemで計算するテスト
        void JobSystemCalculation(float deltaTime)
        {
            // 回転計算用jobの作成
            MyParallelForUpdate rotateJob = new MyParallelForUpdate()
            {
                Accessor = this._dokabenStructs,
                DeltaTime = deltaTime,
            };
            // Jobの実行
            this._jobHandle = rotateJob.Schedule(this._maxObjectNum, this._innerloopBatchCount);
            // Jobが終わるまで処理をブロック
            this._jobHandle.Complete();
        }

        // 回転の算出
        static DokabenStruct Rotate(float deltaTime, DokabenStruct data)
        {
            Matrix4x4 m = Matrix4x4.identity;
            float x = 0f, y = 0f, z = 0f;
            m.SetTRS(new Vector3(x, y, z), Quaternion.identity, Vector3.one);
            if (data.DeltaTimeCounter >= Constants.Interval)
            {
                // 原点を-0.5ずらして下端に設定
                float halfY = y - 0.5f;
                float rot = data.CurrentAngle * Mathf.Deg2Rad;
                float sin = Mathf.Sin(rot);
                float cos = Mathf.Cos(rot);
                // 任意の原点周りにX軸回転を行う
                m.m11 = cos;
                m.m12 = -sin;
                m.m21 = sin;
                m.m22 = cos;
                m.m13 = halfY - halfY * cos + z * sin;
                m.m23 = z - halfY * sin - z * cos;

                data.FrameCounter = data.FrameCounter + 1;
                if (data.FrameCounter >= Constants.Framerate)
                {
                    data.CurrentAngle = -data.CurrentAngle;
                    data.FrameCounter = 0;
                }

                data.DeltaTimeCounter = 0f;
            }
            else
            {
                data.DeltaTimeCounter += deltaTime;
            }
            data.Matrix = m;
            return data;
        }

        #endregion // Private Methods
    }
}
