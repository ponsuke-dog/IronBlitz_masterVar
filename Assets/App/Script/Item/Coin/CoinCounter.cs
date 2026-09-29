//using System.Collections.Generic;
//using UnityEngine;

//public class CoinCounter : MonoBehaviour
//{
//    public static CoinCounter Instance { get; private set; }

//    private CoinRoot coinRoot;
//    private Coin[] coins;
//    private bool[] coinsFlg;

//    public IReadOnlyList<bool> CoinsFalgs => coinsFlg;
//    private void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;

//        coinRoot = FindFirstObjectByType<CoinRoot>();

//        if (coinRoot == null)
//        {
//            Debug.LogError("CoinRootが見つかりません");
//            return;
//        }

//        for (int i = 0; i < coinRoot.transform.childCount; i++)
//        {
//            Coin coin =
//                coinRoot.transform.GetChild(i).GetComponent<Coin>();

//            coin.Initialize(i);
//        }
//    }

//    public void Initialize(StageSaveData stageData)
//    {
//        // シーン内のコイン取得
//        coins = FindObjectsByType<Coin>(FindObjectsSortMode.None);

//        // セーブデータサイズ調整
//        while (stageData.CoinsFlags.Count < coins.Length)
//        {
//            stageData.CoinsFlags.Add(false);
//        }

//        // メモリ上にコピー
//        coinsFlg = stageData.CoinsFlags.ToArray();

//        // Index振り
//        for (int i = 0; i < coins.Length; i++)
//        {
//            coins[i].Initialize(i);

//            // 取得済みなら見た目変更
//            if (coinsFlg[i])
//            {
//             //   coins[i].SetCollectedVisual();
//            }
//        }
//    }

//    public void CoinGetAnnounce(Coin coin)
//    {
//        coinsFlg[coin.CoinIndex] = true;
//    }
//}
