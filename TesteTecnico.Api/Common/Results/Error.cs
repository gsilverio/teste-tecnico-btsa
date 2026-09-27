namespace TesteTecnico.Api.Common.Results;

/// <summary>Descreve uma falha esperada da aplicação ou do domínio.</summary>
/// <param name="Code">Código estável usado pela aplicação para classificar a falha.</param>
/// <param name="Message">Descrição destinada ao tratamento da falha.</param>
public sealed record Error(string Code, string Message);
