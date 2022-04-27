# CopyComponentsByRegex

## 概要

これは正規表現でマッチする、構造が同じ場所にあるコンポーネントを一括でコピーするUnityエディタ拡張です。

## インストール

[このリポジトリのzipファイル](https://github.com/Taremin/CopyComponentsByRegex/archive/master.zip)をダウンロードして、解凍したものをアセット内にコピーします。

### インストール時の注意

ここで注意するのは `Editor` フォルダも **そのまま** コピーすることです。

これはUnityの仕様で「`Editor`フォルダの中にあるスクリプトはエディターでのみ有効で、ゲーム実行時には無視される」というのがあるからです。
(参考: [特殊なフォルダー名 - Unity マニュアル](https://docs.unity3d.com/ja/2018.4/Manual/SpecialFolders.html))

`Editor` フォルダ内の `*.cs` ファイルのみをアセットにいれてしまうと、ゲーム実行時にも実行されてしまいエラーが発生します。

## 使い方

1. ヒエラルキーでコピー元のオブジェクトを選択
2. ヒエラルキーで右クリックしてコンテキストメニューから `Copy Components By Regex` をクリック
3. `Copy Components By Regex` ウィンドウが開くので `正規表現` にコピーしたいコンポーネントとマッチする正規表現を書く
   (例: `Dynamic Bone` と `Dynamic Bone Collider` をコピーしたいなら `Dynamic` など)
4. `Copy Components By Regex` ウィンドウの `Copy` ボタンを押す
5. ヒエラルキーでコピー先のオブジェクトを選択
6. `Copy Components By Regex` ウィンドウの `Paste` ボタンを押す


## 注意

### コピー範囲外のコンポーネントへの参照

コピーするオブジェクトとコンポーネント内で完結しているオブジェクト参照(Dynamic Bone の `root` など)は自動的にコピー先のオブジェクトやコンポーネントに差し替えます。
逆に言えばコピーする範囲外のコンポーネントへの参照はそのままになっているため、注意してください。

### オブジェクト構造の判定

構造の同一性はオブジェクトの名前で判断しているため、同じ親を持つ同名の子オブジェクトがある場合などで動作がおかしくなる可能性があります。
また、完全に構造が同一でなくても子の名前が同じならできるだけ辿ろうとするため、ボーンの増加などの場合もそのままコピーできます。

### Clothコンポーネントのコピー

Cloth コンポーネントのコピーは同じモデル同士で Cloth 部分の頂点数が同じならば、Constraints の単純なコピーが行われます。（高速）
頂点数が変わっていたり、大きく形状が変わっていた場合などは `ClothコンポーネントのConstraintsを一番近い頂点からコピーする` にチェックを入れて使用するとコピーできます。（少し遅い）
「一番近い頂点からコピーする」設定はコピー元とコピー先のそれぞれの頂点の座標を比較して行うのですが、Unity(5.6.3p1, 2017.4.15f1) の Cloth の追加時の頂点座標がおかしいため、あらかじめ Cloth をコピー先に追加しておいてください。


## より詳しい説明

https://taremin.kibe.la/shared/entries/95c1d6cf-9fcd-4a57-8849-677529e50e77 により詳しい説明を書きましたので、もしよければそちらも参考にしてください。


## ライセンス

[MIT](./LICENSE)

### 利用ライブラリ

`CopyComponentsByRegex` では以下のコードを改変して利用しています。

- KDTree.cs - A Stark, September 2009. https://forum.unity.com/threads/point-nearest-neighbour-search-class.29923/
- CopyPasteComponent.cs - tsubaki, November 2015. https://gist.github.com/tsubaki/d049957ad312e3a12764
