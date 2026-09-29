using System;
using UnityEngine;

public class PlayBGMStart : MonoBehaviour
{
    [SerializeField] private string PlayBGM;
    void Start()
    {
        AudioManager.Instance.PlayBgm(PlayBGM);
    }

}
