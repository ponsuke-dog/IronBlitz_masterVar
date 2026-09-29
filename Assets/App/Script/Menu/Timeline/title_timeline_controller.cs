/*
 * title_timeline_controller.cs
 * 
 * タイトル画面の背景演出用Timelineを制御するクラス。
 * ロゴ、メニュー、ConfigなどのUIは制御せず、
 * 背景モデル、キャラクターモデル、カメラ、ライトなどの3D演出のみを担当する。
 */

using UnityEngine;
using UnityEngine.Playables;

namespace IronBlitz.Title
{
    /// <summary>
    /// タイトル画面用Timelineの再生制御を行うクラス。
    /// </summary>
    public sealed class TitleTimelineController : MonoBehaviour
    {
        [Header("Timeline")]
        [SerializeField]
        private PlayableDirector playableDirector;

        [Header("Playback Settings")]
        [SerializeField]
        private bool playOnStart = true;

        [SerializeField]
        private bool restartWhenStopped = true;

        /// <summary>
        /// Timelineが有効かどうか。
        /// </summary>
        private bool isTimelineAvailable;

        private void Awake()
        {
            // PlayableDirectorが未設定の場合は同じGameObjectから取得する。
            if (playableDirector == null)
            {
                playableDirector = GetComponent<PlayableDirector>();
            }

            isTimelineAvailable = playableDirector != null;

            if (!isTimelineAvailable)
            {
                Debug.LogWarning("[TitleTimelineController] PlayableDirectorが設定されていません。");
                return;
            }

            // Timelineが停止した時の処理を登録する。
            playableDirector.stopped += OnTimelineStopped;
        }

        private void Start()
        {
            if (!isTimelineAvailable)
            {
                return;
            }

            if (playOnStart)
            {
                PlayTimeline();
            }
        }

        private void OnDestroy()
        {
            if (playableDirector != null)
            {
                playableDirector.stopped -= OnTimelineStopped;
            }
        }

        /// <summary>
        /// Timelineを再生する。
        /// </summary>
        public void PlayTimeline()
        {
            if (!isTimelineAvailable)
            {
                return;
            }

            playableDirector.Play();
        }

        /// <summary>
        /// Timelineを停止する。
        /// </summary>
        public void StopTimeline()
        {
            if (!isTimelineAvailable)
            {
                return;
            }

            playableDirector.Stop();
        }

        /// <summary>
        /// Timelineを最初から再生し直す。
        /// </summary>
        public void RestartTimeline()
        {
            if (!isTimelineAvailable)
            {
                return;
            }

            playableDirector.time = 0.0;
            playableDirector.Evaluate();
            playableDirector.Play();
        }

        /// <summary>
        /// Timeline停止時の処理。
        /// ループ再生したい場合は最初から再生し直す。
        /// </summary>
        /// <param name="director">停止したPlayableDirector。</param>
        private void OnTimelineStopped(PlayableDirector director)
        {
            if (!restartWhenStopped)
            {
                return;
            }

            if (director != playableDirector)
            {
                return;
            }

            RestartTimeline();
        }
    }
}