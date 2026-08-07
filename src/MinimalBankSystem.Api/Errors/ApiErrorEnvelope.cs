namespace MinimalBankSystem.Api.Errors;

public sealed record ApiErrorEnvelope(string Code, string Message)
{
    public static ApiErrorEnvelope ValidationFailed { get; } = new("validation_failed", "入力内容が正しくありません。");

    public static ApiErrorEnvelope InternalError { get; } = new("internal_error", "内部エラーが発生しました。");
}
