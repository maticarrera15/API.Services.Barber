namespace Api.Services.Barber.Common;

public static class HttpErrors
{
    public static IResult Validation(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Datos inválidos", detail: detail);

    public static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: detail);

    public static IResult Conflict(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflicto", detail: detail);

    public static IResult Unauthorized(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autorizado", detail: detail);

    public static IResult Forbidden(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Prohibido", detail: detail);
}
