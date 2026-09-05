# 日本地震サバイバルゲーム

過去の地震データを時系列で再現しながら、都道府県を移動して生き残るサバイバルゲーム。
このリポジトリは **Phase 1（プロトタイプ）** の実装です。

Phase 1 で実装済みの範囲：

1. 都道府県データの読み込み（`Assets/Resources/Data/prefectures.json`）
2. プレイヤーの現在地表示・隣接県への移動（10日に1回）
3. ゲーム内日付を1日進める
4. 仮の地震データの読み込み（`Assets/Resources/Data/earthquakes.json`）
5. プレイヤーがいる都道府県の震度判定（**震央ではなく観測震度で判定**）
6. 震度5弱以上でGAME OVER
7. 生存日数の表示
8. リスタート

見た目（地図イラストなど）よりゲームシステムを優先しており、都道府県は
ボタンの一覧として表示されます（クリックで移動）。

## 1. 必要なUnity設定

- Unity Editor 2022.3 LTS 系（`ProjectSettings/ProjectVersion.txt` に記載のバージョン、または近いLTS）
- Unity Hub でこのフォルダを「プロジェクトを開く」から開く
- 初回起動時にパッケージが自動でインポートされます（`Packages/manifest.json` 参照）

## 2. フォルダ構成

```
Assets/
├── Scripts/
│   ├── GameManager.cs        … 日付・ゲーム状態・GAME OVER判定を統括
│   ├── PlayerManager.cs      … プレイヤー位置・隣接県・移動クールダウン
│   ├── EarthquakeManager.cs  … 地震データの読み込みと日付照合
│   ├── MapManager.cs         … 都道府県ボタンの生成と表示更新
│   ├── Prefecture.cs         … 都道府県データ用クラス
│   ├── EarthquakeData.cs     … 地震イベント用クラス・震度スケール変換
│   └── MiniJSON.cs           … 辞書を含むJSONを読むための最小JSONパーサ
├── Resources/
│   └── Data/
│       ├── prefectures.json  … 都道府県の隣接関係
│       └── earthquakes.json  … 仮の地震データ（10〜20件）
└── Scenes/
    └── MainScene.unity（後述の手順で作成）
```

データ（`Resources/Data/`）とロジック（`Scripts/`）は分離されており、
将来 `earthquakes.json` を気象庁の実データに差し替えても、
スクリプト側の変更は基本的に不要です。

> **なぜ Resources フォルダに入れる？**
> Unity は `Resources.Load` を使うと、エディタでもビルド後（WebGL含む）でも
> 同じコードでJSONファイルを読み込めます。仕様書のフォルダ例では
> `Data/` を独立させていますが、実行時に読み込めるようにするため
> `Assets/Resources/Data/` に配置しています。中身の分離という意図は
> 保たれています。

## 3. シーンの作成手順（初回のみ）

Unity Editor で以下を行います。

### 3-1. シーンを作成

1. `Assets/Scenes` を右クリック → `Create > Scene` → 名前を `MainScene` にする
2. ダブルクリックして開く

### 3-2. Canvas とUI要素を作成

1. Hierarchy を右クリック → `UI > Canvas` を作成（`EventSystem` も自動生成されます）
2. Canvas の下に、以下の `UI > Text` を作成し、名前を分かりやすく変更する
   - `DateText`（例：「日付：2000年1月1日」）
   - `SurvivalDaysText`（例：「生存日数：0日」）
   - `CurrentPrefectureText`（例：「現在地：東京都」）
   - `NextMoveText`（例：「次回移動可能：あと10日」）
   - `LatestEarthquakeText`（例：「最新の地震：なし」、複数行なので少し大きめのRect Transformにする）
3. Canvas の下に `UI > Button` を作成し、名前を `AdvanceDayButton` にする。子の Text を「次の日へ」にする
4. Canvas の下に空の `GameObject` を作成し、`GameOverPanel` という名前にする
   - この中に `UI > Text` を追加し「GAME OVER」などと表示する
   - Inspector 右上の有効化チェックボックスを外し、非アクティブにしておく（ゲーム開始時は非表示）

### 3-3. 都道府県ボタン一覧（MapManager用）

1. Canvas の下に空の `GameObject` を作成し `ButtonContainer` という名前にする
2. `ButtonContainer` に `Grid Layout Group` コンポーネントを追加する（Cell Size は 100x40 程度）
3. `ButtonContainer` の下に `UI > Button` を1つ作成し、名前を `PrefectureButtonPrefab` にする
4. `PrefectureButtonPrefab` を `Assets/Resources` などにドラッグしてプレハブ化した後、
   Hierarchy 上のオブジェクトは削除してよい（プレハブだけ残す）
   - あるいはシーン上に残したまま非アクティブにしてプレハブ参照用にしてもよい

### 3-4. マネージャー用GameObjectを作成

1. 空の `GameObject` を3つ作成し、それぞれ名前を付ける：
   - `GameManager`
   - `PlayerManager`
   - `EarthquakeManager`
   - `MapManager`
2. それぞれに対応するスクリプトをアタッチする
   - `GameManager` オブジェクト → `GameManager.cs`
   - `PlayerManager` オブジェクト → `PlayerManager.cs`
   - `EarthquakeManager` オブジェクト → `EarthquakeManager.cs`
   - `MapManager` オブジェクト → `MapManager.cs`

### 3-5. Inspector で参照を接続

`GameManager` の Inspector で、以下のフィールドにドラッグ＆ドロップで接続する：

- `Player Manager` → `PlayerManager` オブジェクト
- `Earthquake Manager` → `EarthquakeManager` オブジェクト
- `Map Manager` → `MapManager` オブジェクト
- `Date Text` / `Survival Days Text` / `Current Prefecture Text` / `Next Move Text` / `Latest Earthquake Text` → 対応するTextオブジェクト
- `Game Over Panel` → `GameOverPanel`
- `Advance Day Button` → `AdvanceDayButton`

`MapManager` の Inspector で：

- `Prefecture Button Prefab` → 3-3で作成した `PrefectureButtonPrefab`
- `Button Container` → `ButtonContainer`

`AdvanceDayButton` の `OnClick()` に `GameManager.OnAdvanceDayClicked()` を追加する
（Button の Inspector → `On Click ()` → `+` → `GameManager` オブジェクトをドラッグ →
関数選択で `GameManager > OnAdvanceDayClicked` を選ぶ）。

GameOverPanel内に「リスタート」ボタンを追加する場合は、その `OnClick()` に
`GameManager.OnRestartClicked()` を割り当てる。

## 4. 動作確認方法

1. Unity Editor 上部の再生ボタンで実行する
2. 画面に日付・生存日数・現在地・次回移動可能日数・最新の地震欄が表示される
3. `ButtonContainer` に都道府県ボタンが並び、現在地はオレンジ、
   移動可能な隣接県は緑で表示される（`MapManager.Refresh` の色設定）
4. 「次の日へ」ボタンを押すと日付が1日進み、`earthquakes.json` にその日付の
   データがあれば最新の地震欄に表示される
5. プレイヤーのいる都道府県で震度5弱以上を観測すると、
   `GameOverPanel` が表示され、「次の日へ」ボタンが押せなくなる
6. リスタートボタン（実装していれば）でゲーム状態が初期化される

`Assets/Resources/Data/earthquakes.json` の日付・震度を編集して、
狙った条件で GAME OVER になるか確認すると動作確認がしやすい。

## 5. 今後の拡張（Phase 2以降）

- 占い師システム（`EarthquakeManager.PeekFutureEarthquake` を利用）
- スコア・生存日数のランキング
- 気象庁の実データへの差し替え（`earthquakes.json` の形式はそのまま拡張可能）
- 日本地図の見た目の改善（都道府県ボタン一覧 → 実際の地図上での配置へ）
- WebGLビルドを作成し、unityroomへアップロード

## 6. ライセンス・データについて

`Assets/Resources/Data/earthquakes.json` は Phase 1 用の架空データです。
実データに差し替える際は気象庁の震度データベースの利用条件を確認してください。
（https://www.data.jma.go.jp/eqev/data/bulletin/shindo.html）

実際の災害（東日本大震災など）を題材とするため、死者数などをスコア化する
表現は避け、GAME OVER画面などの表現にも配慮すること。
