# MainScene 詳細セットアップ手順（座標付き）

README.md の「3. シーンの作成手順」の詳細版です。数値通りに設定すれば、
画面が崩れずに表示されます（解像度 1280x720 想定 / Canvas Scaler は
「Scale With Screen Size」・Reference Resolution 1280x720 を推奨）。

すべて **Rect Transform** の `Pos X / Pos Y / Width / Height` を Inspector
で直接入力してください（Anchor は特に指定がなければ既定の中央のままでOK）。

---

## 0. 事前準備

1. `Assets/Scenes` フォルダ内で右クリック → `Create > Scene` → `MainScene`
2. ダブルクリックして開く
3. Hierarchy を右クリック → `UI > Canvas` を作成（`EventSystem` も自動生成）
4. `Canvas` を選択 → Inspector の `Canvas Scaler` コンポーネントで
   - `UI Scale Mode` → `Scale With Screen Size`
   - `Reference Resolution` → `X: 1280, Y: 720`

---

## 1. テキスト要素（Canvas の直下に作成）

`Canvas` を右クリック → `UI > Text`（Legacy）を選び、以下の値で作成する。
5つとも同じ手順で、名前と座標だけ変える。

| GameObject名 | Pos X | Pos Y | Width | Height | Font Size | 初期テキスト |
|---|---:|---:|---:|---:|---:|---|
| `DateText` | -450 | 330 | 400 | 40 | 24 | 日付：2000年1月1日 |
| `SurvivalDaysText` | -450 | 290 | 400 | 40 | 24 | 生存日数：0日 |
| `CurrentPrefectureText` | -450 | 250 | 400 | 40 | 24 | 現在地：東京都 |
| `NextMoveText` | -450 | 210 | 400 | 40 | 24 | 次回移動可能：あと10日 |
| `LatestEarthquakeText` | 250 | 150 | 480 | 260 | 20 | 最新の地震：なし |

補足：
- Pos X/Y は Canvas 中央を (0,0) とした相対座標です（Anchor: 中央のまま）。
- `LatestEarthquakeText` は複数行表示するため、Inspector の Text コンポーネントで
  `Horizontal Overflow` → `Wrap`、`Vertical Overflow` → `Overflow` にしておく。

---

## 2. 「次の日へ」ボタン

1. `Canvas` を右クリック → `UI > Button`（Legacy）
2. 名前を `AdvanceDayButton` に変更
3. Rect Transform: `Pos X: -450, Pos Y: -280, Width: 200, Height: 60`
4. 子の `Text` を選択し、内容を「次の日へ」に変更、Font Size 28

---

## 3. GAME OVER パネル

1. `Canvas` を右クリック → `Create Empty` → 名前を `GameOverPanel` に変更
2. Rect Transform: `Pos X: 0, Pos Y: 0, Width: 500, Height: 300`（画面中央に大きく表示）
3. `GameOverPanel` に `Image` コンポーネントを追加し、Color を半透明の黒
   （例: R0 G0 B0 A200）にして背景を暗くする
4. `GameOverPanel` の子として `UI > Text` を追加
   - 名前: `GameOverText`
   - Rect Transform: `Pos X: 0, Pos Y: 60, Width: 460, Height: 100`
   - Font Size 36、テキストは `GAME OVER`（または「生存失敗」）
5. `GameOverPanel` の子として `UI > Button` を追加
   - 名前: `RestartButton`
   - Rect Transform: `Pos X: 0, Pos Y: -60, Width: 200, Height: 60`
   - 子Textを「リスタート」に変更
6. `GameOverPanel` を選択し、Inspector 右上のチェックボックスを **オフ**にして
   非アクティブ状態で保存する（ゲーム開始時は非表示のため）

---

## 4. 都道府県ボタン一覧（MapManager用）

### 4-1. ボタンを並べるコンテナ

1. `Canvas` を右クリック → `Create Empty` → 名前を `ButtonContainer` に変更
2. Rect Transform: `Pos X: 250, Pos Y: -80, Width: 480, Height: 380`
3. `Add Component` → `Grid Layout Group`
   - `Cell Size`: `X: 110, Y: 36`
   - `Spacing`: `X: 6, Y: 6`
   - `Constraint`: `Fixed Column Count` → `4`
4. `Add Component` → `Scroll Rect` は任意（46件のボタンが `ButtonContainer` の
   高さに収まらない場合は、`ButtonContainer` を `UI > Scroll View` の
   `Content` オブジェクトとして差し替えても良い。Phase 1では省略可）

### 4-2. ボタンのプレハブ

1. `ButtonContainer` の下に `UI > Button`（Legacy）を作成
2. 名前を `PrefectureButtonPrefab` に変更
3. Rect Transform: `Width: 110, Height: 36`（Grid Layout Group が位置を自動配置するので Pos X/Y は無視されます）
4. 子の `Text` の Font Size を 16 にしておく（都道府県名がボタン内に収まるように）
5. `Assets` フォルダ内に `Prefabs` フォルダを作成し、`PrefectureButtonPrefab` を
   そこへドラッグ＆ドロップしてプレハブ化する
6. プレハブ化後、Hierarchy 上の `PrefectureButtonPrefab` は削除してよい
   （`MapManager` はプレハブ資産（Projectウィンドウ側）を参照するため）

---

## 5. マネージャー用 GameObject

1. Hierarchy を右クリック → `Create Empty` を4回行い、それぞれ以下の名前にする
   - `GameManager`
   - `PlayerManager`
   - `EarthquakeManager`
   - `MapManager`
   （座標は画面に表示されないオブジェクトなので位置は任意で構いません）
2. それぞれに対応するスクリプトを `Add Component` でアタッチする
   - `GameManager` → `Game Manager` (GameManager.cs)
   - `PlayerManager` → `Player Manager` (PlayerManager.cs)
   - `EarthquakeManager` → `Earthquake Manager` (EarthquakeManager.cs)
   - `MapManager` → `Map Manager` (MapManager.cs)

---

## 6. Inspector 上での参照接続

### GameManager

Hierarchy で `GameManager` を選択し、Inspector で以下をドラッグ＆ドロップ：

| フィールド | 接続するオブジェクト |
|---|---|
| Player Manager | `PlayerManager` |
| Earthquake Manager | `EarthquakeManager` |
| Map Manager | `MapManager` |
| Date Text | `DateText` |
| Survival Days Text | `SurvivalDaysText` |
| Current Prefecture Text | `CurrentPrefectureText` |
| Next Move Text | `NextMoveText` |
| Latest Earthquake Text | `LatestEarthquakeText` |
| Game Over Panel | `GameOverPanel` |
| Advance Day Button | `AdvanceDayButton` |

その他の値は初期値のままでOK（`Start Date String`: 2000-01-01、
`Starting Prefecture`: 東京都、`Game Over Intensity Rank`: 5）。

### MapManager

| フィールド | 接続するオブジェクト |
|---|---|
| Prefecture Button Prefab | Project内の `PrefectureButtonPrefab`（Assets/Prefabs） |
| Button Container | `ButtonContainer` |

---

## 7. ボタンのクリックイベント設定

### AdvanceDayButton

1. `AdvanceDayButton` を選択 → Inspector の `Button` コンポーネント →
   `On Click ()` セクションの `+` をクリック
2. 空欄に Hierarchy から `GameManager` オブジェクトをドラッグ
3. 右側のドロップダウンで `GameManager > OnAdvanceDayClicked ()` を選択

### RestartButton（GameOverPanel内）

1. `RestartButton` を選択 → `On Click ()` の `+`
2. `GameManager` をドラッグ
3. `GameManager > OnRestartClicked ()` を選択

※ `MapManager.cs` 側で都道府県ボタンの `onClick` はスクリプトから
  自動的に登録されるため、Inspector側での設定は不要です。

---

## 8. 動作確認チェックリスト

再生ボタンを押して、以下を順に確認してください。

- [ ] `DateText` に「日付：2000年1月1日」等が表示される
- [ ] `ButtonContainer` に46都道府県分のボタンが並ぶ
- [ ] 現在地（初期値：東京都）のボタンがオレンジ色になっている
- [ ] 東京都に隣接する県（埼玉県・千葉県・神奈川県・山梨県）のボタンが
      薄緑色になっている（10日間は移動不可のため、実際に移動できるのは
      10日目以降）
- [ ] 「次の日へ」を押すと `DateText` と `SurvivalDaysText` が更新される
- [ ] 移動可能な県のボタンを押すと `CurrentPrefectureText` が変わり、
      `NextMoveText` が「あと10日」にリセットされる
- [ ] `Assets/Resources/Data/earthquakes.json` の日付になった時、
      `LatestEarthquakeText` に震央・M・観測震度が表示される
- [ ] プレイヤーのいる県で震度5弱以上を観測すると `GameOverPanel` が表示され、
      「次の日へ」ボタンが押せなくなる
- [ ] `RestartButton` を押すとゲームが最初からやり直せる

すべて確認できたら、`File > Build Settings` から WebGL ビルドを行い、
unityroom へアップロードしてください（README.md 5節を参照）。
