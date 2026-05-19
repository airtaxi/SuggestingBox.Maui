# 마이그레이션 가이드

## v1에서 v2로

SuggestingBox.Maui v2에서는 이미지 붙여넣기가 단순 알림 이벤트에서 명시적 인라인 이미지 토큰 방식으로 바뀌었습니다. mention과 이미지는 이제 `GetTokens()`로 추출하고 `SetContent(...)`로 복원하는 같은 token 모델을 공유합니다.

### 이미지 붙여넣기 이벤트

`ImageInserted`와 `ImageInsertedCommand`는 `ImagePasteRequested`와 `ImagePasteRequestedCommand`로 대체되었습니다.

v1:

```csharp
SuggestingBoxControl.ImageInserted += (sender, args) =>
{
    PastedImage.Source = ImageSource.FromStream(() => new MemoryStream(args.ImageData));
};
```

v2:

```csharp
SuggestingBoxControl.ImagePasteRequested += (sender, args) =>
{
    // 여기에서 이미지 검증, 리사이즈, 업로드, 거절 등을 결정합니다.
    args.AlternativeText = "Pasted image";
    args.WidthRequest = 180;
    args.InsertImageImmediately = true;
};
```

핸들러에서 `InsertImageImmediately`를 설정하거나 `InsertImageToken(...)`을 호출하지 않으면 붙여넣은 이미지는 에디터에 삽입되지 않습니다.

### 토큰 데이터

`SuggestingBoxTokenInfo`에 `Kind`가 추가되었습니다.

- `Kind == SuggestingBoxTokenKind.Mention`은 기존 mention/token 동작을 의미합니다.
- `Kind == SuggestingBoxTokenKind.Image`는 인라인 이미지 토큰을 의미합니다.
- 이미지 토큰은 `Text` 안에서 `\uFFFC` 한 글자를 차지합니다.
- token range는 `Prefix.Length + DisplayText.Length`로 직접 계산하지 말고 `Length`와 `EndIndex`를 사용하세요.

v1:

```csharp
foreach (var token in SuggestingBoxControl.GetTokens())
{
    Console.WriteLine($"{token.Prefix}{token.DisplayText}");
}
```

v2:

```csharp
foreach (var token in SuggestingBoxControl.GetTokens())
{
    Console.WriteLine(token.Kind == SuggestingBoxTokenKind.Image
        ? $"image ({token.ContentType})"
        : $"{token.Prefix}{token.DisplayText}");
}
```

### 콘텐츠 복원

복원 API는 계속 `SetContent(string text, IEnumerable<SuggestingBoxTokenInfo> tokens)`입니다. 이미지 토큰의 경우 해당 위치의 text에 `\uFFFC`가 있어야 합니다. 누락되어 있으면 v2가 복원 중 자동으로 삽입합니다.

### 이미지 content type과 크기

`InsertImageToken(...)`의 `contentType`은 optional입니다. 생략하면 라이브러리가 이미지 bytes에서 일반적인 이미지 형식을 감지합니다.

이미지 토큰에는 optional 문자열 `Tag`도 지정할 수 있습니다. `InsertImageToken(..., tag: "...")`, `SuggestingBoxTokenInfo.CreateImage(..., tag: "...")`, 또는 `ImagePasteRequestedEventArgs.Tag`를 사용할 수 있습니다.

`widthRequest`와 `heightRequest`가 모두 `-1`이면 원본 이미지 크기를 사용합니다. 한쪽만 지정하면 원본 비율로 나머지를 계산합니다.
