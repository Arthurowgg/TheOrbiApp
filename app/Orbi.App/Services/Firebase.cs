using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orbi.Services;

/// <summary>Sessão do Firebase Authentication (mesma conta do site).</summary>
public sealed class FirebaseSession
{
    public string IdToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string Uid { get; set; } = "";
    public string Email { get; set; } = "";
    public long ExpiresAtUnix { get; set; }
    /// <summary>"google.com" ou "password" — de onde veio a conta.</summary>
    public string Provider { get; set; } = "password";
    public string DisplayName { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
    public bool EmailVerified { get; set; }

    public bool Expired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() > ExpiresAtUnix - 120;
}

/// <summary>
/// Cliente mínimo do Firebase para o app: Authentication (REST) e Firestore (REST).
///
/// Por que REST e não o SDK: o app é .NET nativo. O SDK oficial é para Node/Java/
/// Python/Go; a API REST do Firebase é a mesma coisa que o site usa por baixo do
/// SDK web, e com ela o app fala com o MESMO projeto — mesma conta, mesmas regras,
/// mesmos dados.
/// </summary>
public static class Firebase
{
    /* ------------------------- configuração pública ------------------------- */
    // Estes valores são públicos (identificam o projeto). O que protege os dados
    // são o Authentication e as regras do Firestore.
    public const string ApiKey = "AIzaSyDCpoYpdBiu_ENT8xFbvz0y1gl0W1CufFw";
    public const string ProjectId = "orbi-app-auth";
    public const string SiteUrl = "https://orbi-app-auth.web.app";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static FirebaseSession? Session { get; private set; }
    public static bool SignedIn => Session is not null;
    public static string? Email => Session?.Email;
    public static string? Uid => Session?.Uid;

    public static event Action? Changed;

    /* ------------------------------ sessão ------------------------------ */
    static Firebase()
    {
        var saved = Store.LoadSession();
        if (saved is not null) Session = saved;
    }

    private static void SetSession(FirebaseSession session)
    {
        Session = session;
        Store.SaveSession(session);
        Changed?.Invoke();
    }

    /// <summary>Deixa a sessão pronta (usado pelo login do Google, que monta os tokens fora).</summary>
    public static void AdoptSession(FirebaseSession session)
    {
        SetSession(session);
        Store.Current.AccountEmail = session.Email;
        Store.Save();
    }

    public static void SignOut()
    {
        Session = null;
        Store.SaveSession(null);
        Changed?.Invoke();
    }

    /* ------------------------------ contas ------------------------------ */
    public static Task<(bool Ok, string Message)> SignInAsync(string email, string password)
        => AuthAsync("accounts:signInWithPassword", email, password);

    public static Task<(bool Ok, string Message)> SignUpAsync(string email, string password)
        => AuthAsync("accounts:signUp", email, password);

    private static async Task<(bool, string)> AuthAsync(string endpoint, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return (false, "Digite um e-mail válido.");
        }
        if (password.Length < 6)
        {
            return (false, "A senha precisa de pelo menos 6 caracteres.");
        }

        try
        {
            var payload = new JsonObject
            {
                ["email"] = email.Trim().ToLowerInvariant(),
                ["password"] = password,
                ["returnSecureToken"] = true,
            };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/{endpoint}?key={ApiKey}", content);

            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return (false, TranslateAuthError(json, response.StatusCode));
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var session = new FirebaseSession
            {
                IdToken = root.GetProperty("idToken").GetString() ?? "",
                RefreshToken = root.GetProperty("refreshToken").GetString() ?? "",
                Uid = root.GetProperty("localId").GetString() ?? "",
                Email = root.GetProperty("email").GetString() ?? email.Trim().ToLowerInvariant(),
                ExpiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ParseSeconds(root, "expiresIn", 3600),
                Provider = "password",
                DisplayName = root.TryGetProperty("displayName", out var nome) ? nome.GetString() ?? "" : "",
                PhotoUrl = root.TryGetProperty("photoUrl", out var foto) ? foto.GetString() ?? "" : "",
                EmailVerified = root.TryGetProperty("emailVerified", out var visto) && visto.ValueKind == JsonValueKind.True,
            };
            SetSession(session);
            Store.Current.AccountEmail = session.Email;
            Store.Save();

            return (true, endpoint.EndsWith("signUp", StringComparison.Ordinal)
                ? "Conta criada e vinculada a este PC."
                : "Entrou na conta. A sincronização começa agora.");
        }
        catch (Exception error)
        {
            Log.Error("entrar na conta", error);
            return (false, "Sem conexão com a conta. Confira a internet e tente de novo.");
        }
    }

    public static async Task<(bool Ok, string Message)> SendResetAsync(string email)
    {
        try
        {
            var payload = new JsonObject { ["requestType"] = "PASSWORD_RESET", ["email"] = email.Trim().ToLowerInvariant() };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={ApiKey}", content);
            if (response.IsSuccessStatusCode) return (true, $"Link de redefinição enviado para {email}.");
            var json = await response.Content.ReadAsStringAsync();
            return (false, TranslateAuthError(json, response.StatusCode));
        }
        catch (Exception error)
        {
            return (false, error.Message);
        }
    }

    private static string TranslateAuthError(string json, HttpStatusCode status)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var code = document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? "";
            return code switch
            {
                "EMAIL_NOT_FOUND" or "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" =>
                    "E-mail ou senha incorretos.",
                "EMAIL_EXISTS" => "Já existe uma conta com este e-mail. Use Entrar.",
                "WEAK_PASSWORD : Password should be at least 6 characters" =>
                    "A senha precisa de pelo menos 6 caracteres.",
                "OPERATION_NOT_ALLOWED" => "O login por e-mail não está disponível agora.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Muitas tentativas. Espere um minuto.",
                "NETWORK_REQUEST_FAILED" => "Sem conexão agora.",
                _ when status == HttpStatusCode.BadRequest => "Confira o e-mail e a senha.",
                _ => "Não consegui falar com a conta agora.",
            };
        }
        catch
        {
            return "Não consegui falar com a conta agora.";
        }
    }

    private static async Task<bool> RefreshAsync()
    {
        if (Session is null || string.IsNullOrWhiteSpace(Session.RefreshToken)) return false;
        try
        {
            var payload = new JsonObject { ["grant_type"] = "refresh_token", ["refresh_token"] = Session.RefreshToken };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://securetoken.googleapis.com/v1/token?key={ApiKey}", content);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Session.IdToken = root.GetProperty("id_token").GetString() ?? Session.IdToken;
            Session.RefreshToken = root.GetProperty("refresh_token").GetString() ?? Session.RefreshToken;
            Session.ExpiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ParseSeconds(root, "expires_in", 3600);
            Store.SaveSession(Session);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>expiresIn vem como string no Auth e como number no securetoken.</summary>
    private static int ParseSeconds(JsonElement root, string name, int fallback)
    {
        if (!root.TryGetProperty(name, out var node)) return fallback;
        if (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out var text)) return text;
        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var number)) return number;
        return fallback;
    }

    private static async Task<string?> TokenAsync()
    {
        if (Session is null) return null;
        if (Session.Expired && !await RefreshAsync())
        {
            return null;
        }
        return Session.IdToken;
    }

    /* ------------------------------ Firestore ------------------------------ */
    private static string DocUrl() =>
        $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{Session?.Uid}";

    /// <summary>Lê users/{uid}. Devolve null se não existir ou sem permissão.</summary>
    public static async Task<JsonObject?> GetDocAsync()
    {
        var token = await TokenAsync();
        if (token is null) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, DocUrl());
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonNode.Parse(json)?.AsObject();
            return parsed?["fields"]?.AsObject();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Grava campos específicos em users/{uid} usando updateMask (PATCH parcial) —
    /// nunca sobrescreve o que o site gravou em outros campos.
    /// </summary>
    public static async Task<(bool Ok, string Message)> PatchAsync(JsonObject fields)
    {
        var token = await TokenAsync();
        if (token is null) return (false, "Entre na conta para sincronizar.");

        var keys = fields.Select(pair => pair.Key).ToList();
        var mask = string.Join("&", keys.Select(key => $"updateMask.fieldPaths={Uri.EscapeDataString(key)}"));
        var body = new JsonObject { ["fields"] = fields.DeepClone() };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{DocUrl()}?{mask}")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);

            if (response.IsSuccessStatusCode) return (true, "Sincronizado.");

            var json = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Documento ainda não existe: cria com o mínimo obrigatório.
                return await CreateAsync(fields, token);
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, "A conta não aceitou a gravação agora.");
            }
            return (false, "Não deu para salvar na conta agora.");
        }
        catch (Exception error)
        {
            return (false, $"Sem conexão: {error.Message}");
        }
    }

    private static async Task<(bool, string)> CreateAsync(JsonObject fields, string token)
    {
        var payloadFields = (JsonObject)fields.DeepClone();
        payloadFields["createdAt"] = Timestamp(DateTime.UtcNow);
        payloadFields["name"] ??= Str(Store.Current.UserName);
        payloadFields["email"] ??= Str(Session?.Email ?? "");
        payloadFields["color"] ??= Str("#c7452a");
        var payload = new JsonObject { ["fields"] = payloadFields };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, DocUrl())
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode
                ? (true, "Documento criado na sua conta.")
                : (false, "Não deu para ler a conta agora.");
        }
        catch (Exception error)
        {
            return (false, error.Message);
        }
    }

    private static string Trim(string json) => json.Length > 160 ? json[..160] + "…" : json;

    /* ------------------------- perfil e segurança ------------------------- */

    /// <summary>Nome e foto no perfil da conta. O nome também vai para users/{uid}.</summary>
    public static async Task<(bool Ok, string Message)> SaveProfileAsync(string name, string photoUrl)
    {
        var token = await TokenAsync();
        if (token is null) return (false, "Entre na conta primeiro.");
        try
        {
            var payload = new JsonObject
            {
                ["displayName"] = name.Trim(),
                ["photoUrl"] = photoUrl.Trim(),
                ["returnSecureToken"] = true,
            };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={ApiKey}", content);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return (false, TranslateAuthError(json, response.StatusCode));

            if (Session is not null)
            {
                Session.DisplayName = name.Trim();
                Session.PhotoUrl = photoUrl.Trim();
                Store.SaveSession(Session);
            }
            await PatchAsync(new JsonObject
            {
                ["name"] = Str(name.Trim()),
                ["photoUrl"] = Str(photoUrl.Trim()),
            });
            Changed?.Invoke();
            return (true, "Perfil salvo.");
        }
        catch (Exception error)
        {
            Log.Error("salvar perfil", error);
            return (false, "Não consegui salvar agora.");
        }
    }

    public static async Task<(bool Ok, string Message)> ChangePasswordAsync(string current, string next)
    {
        if (Session is null) return (false, "Entre na conta primeiro.");
        if (next.Length < 6) return (false, "A senha nova precisa de pelo menos 6 caracteres.");

        var (entrou, mensagem) = await SignInAsync(Session.Email, current);
        if (!entrou) return (false, "A senha atual não confere.");
        _ = mensagem;

        var token = await TokenAsync();
        if (token is null) return (false, "Entre na conta primeiro.");
        try
        {
            var payload = new JsonObject { ["password"] = next, ["returnSecureToken"] = true };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={ApiKey}", content);
            var json = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Senha trocada.")
                : (false, TranslateAuthError(json, response.StatusCode));
        }
        catch (Exception error)
        {
            Log.Error("trocar senha", error);
            return (false, "Não consegui trocar a senha agora.");
        }
    }

    /// <summary>Manda o e-mail de confirmação para o endereço da conta.</summary>
    public static async Task<(bool Ok, string Message)> SendVerificationAsync()
    {
        var token = await TokenAsync();
        if (token is null || Session is null) return (false, "Entre na conta primeiro.");
        if (Session.EmailVerified) return (true, "Este e-mail já está confirmado.");
        try
        {
            var payload = new JsonObject { ["requestType"] = "VERIFY_EMAIL", ["idToken"] = token };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={ApiKey}", content);
            var json = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Link enviado para " + Session.Email + ".")
                : (false, TranslateAuthError(json, response.StatusCode));
        }
        catch (Exception error)
        {
            Log.Error("enviar verificação", error);
            return (false, "Não consegui enviar o link agora.");
        }
    }

    /// <summary>Relê a conta para saber se o e-mail foi confirmado (o link chega por e-mail).</summary>
    public static async Task<bool> RefreshProfileAsync()
    {
        var token = await TokenAsync();
        if (token is null || Session is null) return false;
        try
        {
            var payload = new JsonObject { ["idToken"] = token };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={ApiKey}", content);
            if (!response.IsSuccessStatusCode) return false;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("users", out var users) || users.GetArrayLength() == 0) return false;
            var user = users[0];
            Session.EmailVerified = user.TryGetProperty("emailVerified", out var visto) && visto.ValueKind == JsonValueKind.True;
            Session.DisplayName = user.TryGetProperty("displayName", out var nome) ? nome.GetString() ?? "" : "";
            Session.PhotoUrl = user.TryGetProperty("photoUrl", out var foto) ? foto.GetString() ?? "" : "";
            Store.SaveSession(Session);
            Changed?.Invoke();
            return Session.EmailVerified;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Apaga a conta de vez (Auth + documento). Exige a sessão recente.</summary>
    public static async Task<(bool Ok, string Message)> DeleteAccountAsync()
    {
        var token = await TokenAsync();
        if (token is null) return (false, "Entre na conta primeiro.");
        try
        {
            // 1. o documento: se falhar, a conta continua inteira
            using (var request = new HttpRequestMessage(HttpMethod.Delete, DocUrl()))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
                using var resposta = await Client.SendAsync(request);
                if (!resposta.IsSuccessStatusCode && resposta.StatusCode != HttpStatusCode.NotFound)
                {
                    return (false, "Não consegui apagar os dados da conta agora.");
                }
            }

            // 2. a conta em si
            var payload = new JsonObject { ["idToken"] = token };
            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await Client.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:delete?key={ApiKey}", content);
            if (!response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return (false, TranslateAuthError(json, response.StatusCode));
            }

            SignOut();
            Memory.Forget();
            return (true, "Conta apagada.");
        }
        catch (Exception error)
        {
            Log.Error("apagar conta", error);
            return (false, "Não consegui apagar a conta agora.");
        }
    }

    /* ------------------------------ aparelhos ------------------------------ */

    private static string DevicesUrl() =>
        $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{Session?.Uid}/devices";

    public sealed record Aparelho(string Id, string Nome, string UltimoAcesso, bool Atual);

    /// <summary>
    /// Aparelhos que usam esta conta. O app registra o próprio a cada entrada;
    /// os outros vêm do que o site gravou.
    /// </summary>
    public static async Task<List<Aparelho>> GetDevicesAsync()
    {
        var lista = new List<Aparelho>();
        var token = await TokenAsync();
        if (token is null) return lista;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, DevicesUrl() + "?pageSize=20");
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return lista;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("documents", out var documentos)) return lista;
            foreach (var documento in documentos.EnumerateArray())
            {
                var id = documento.TryGetProperty("name", out var nome) ? nome.GetString()?.Split('/').Last() ?? "" : "";
                // Os campos vêm como JSON do Firestore; ReadString trabalha com JsonObject.
                var campos = documento.TryGetProperty("fields", out var f)
                    ? JsonNode.Parse(f.GetRawText())?.AsObject()
                    : null;
                lista.Add(new Aparelho(
                    id,
                    ReadString(campos, "name", "Aparelho"),
                    ReadString(campos, "lastSeen", ""),
                    ReadString(campos, "kind", "") == "app"));
            }
        }
        catch (Exception error)
        {
            Log.Error("listar aparelhos", error);
        }
        return lista;
    }

    /// <summary>Marca este PC como um dos aparelhos da conta (sem duplicar).</summary>
    public static async Task RegisterDeviceAsync()
    {
        var token = await TokenAsync();
        if (token is null) return;
        try
        {
            var url = DevicesUrl() + "/" + Environment.MachineName.ToLowerInvariant().Replace(' ', '-') +
                      "?updateMask.fieldPaths=name&updateMask.fieldPaths=kind&updateMask.fieldPaths=lastSeen";
            var body = new JsonObject
            {
                ["fields"] = new JsonObject
                {
                    ["name"] = Str(Environment.MachineName),
                    ["kind"] = Str("app"),
                    ["lastSeen"] = Str(DateTime.Now.ToString("dd/MM/yyyy HH:mm")),
                },
            };
            using var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode) Log.Add("aparelho não registrado: " + (int)response.StatusCode);
        }
        catch (Exception error)
        {
            Log.Error("registrar aparelho", error);
        }
    }

    /// <summary>Remove um aparelho da lista (não derruba a sessão dele em outro PC).</summary>
    public static async Task<(bool Ok, string Message)> ForgetDeviceAsync(string id)
    {
        var token = await TokenAsync();
        if (token is null) return (false, "Entre na conta primeiro.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, DevicesUrl() + "/" + Uri.EscapeDataString(id));
            request.Headers.Add("Authorization", $"Bearer {token}");
            using var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound
                ? (true, "Aparelho removido da lista.")
                : (false, "Não consegui remover esse aparelho.");
        }
        catch (Exception error)
        {
            Log.Error("remover aparelho", error);
            return (false, "Não consegui remover esse aparelho.");
        }
    }

    /* ------------------------- conversão de valores ------------------------- */
    public static JsonNode Str(string value) => new JsonObject { ["stringValue"] = value ?? "" };
    public static JsonNode Bool(bool value) => new JsonObject { ["booleanValue"] = value };
    public static JsonNode Num(double value) => new JsonObject { ["integerValue"] = ((long)Math.Round(value)).ToString() };
    public static JsonNode Timestamp(DateTime value) =>
        new JsonObject { ["timestampValue"] = value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ") };
    public static JsonNode Map(JsonObject fields) => new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = fields } };

    public static JsonNode List(IEnumerable<JsonNode> items) =>
        new JsonObject { ["arrayValue"] = new JsonObject { ["values"] = new JsonArray(items.ToArray()) } };

    /* ------------------------------ leitura ------------------------------ */
    public static string ReadString(JsonObject? fields, string key, string fallback = "")
    {
        if (fields?[key] is JsonObject node && node["stringValue"] is JsonValue value)
        {
            return value.GetValue<string>();
        }
        return fallback;
    }

    public static JsonObject? ReadMap(JsonObject? fields, string key)
    {
        return fields?[key]?["mapValue"]?["fields"] as JsonObject;
    }

    public static IEnumerable<JsonNode> ReadList(JsonObject? fields, string key)
    {
        if (fields?[key]?["arrayValue"]?["values"] is JsonArray array)
        {
            return array.Where(item => item is not null).Select(item => item!);
        }
        return Array.Empty<JsonNode>();
    }

    public static bool ReadBool(JsonObject? fields, string key, bool fallback = false)
    {
        return fields?[key]?["booleanValue"] is JsonValue value ? value.GetValue<bool>() : fallback;
    }

    public static int ReadInt(JsonObject? fields, string key, int fallback = 0)
    {
        var raw = fields?[key]?["integerValue"]?.GetValue<string>();
        return int.TryParse(raw, out var value) ? value : fallback;
    }
}
