using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Orbi.Services;

public enum ReplyKind
{
    Local,
    Confirm,
    NeedsAi,
    Private,
}

public sealed record OrbiReply(string Text, ReplyKind Kind = ReplyKind.Local, string? Detail = null)
{
    public static OrbiReply Local(string text, string? detail = null) => new(text, ReplyKind.Local, detail);
    public static OrbiReply Confirm(string text, string? detail = null) => new(text, ReplyKind.Confirm, detail);
    public static OrbiReply Private(string text, string? detail = null) => new(text, ReplyKind.Private, detail);
    public static OrbiReply Ai(string text, string? detail = null) => new(text, ReplyKind.NeedsAi, detail);
}

/// <summary>
/// Interpretador local: entende os comandos do app sem internet e sem IA.
/// O que não reconhece volta como <see cref="ReplyKind.NeedsAi"/> e vai para o Gemini.
/// </summary>
public static partial class Interpreter
{
    private static string Normalize(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        var normalized = lowered.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        }
        return Whitespace().Replace(builder.ToString(), " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?<n>\d{1,3})\s*%?")]
    private static partial Regex Number();

    [GeneratedRegex(@"(\d+)\s*(min|minuto|minutos|m\b|h|hora|horas)")]
    private static partial Regex Duration();

    private static int? Minutes(string text)
    {
        var match = Duration().Match(text);
        if (!match.Success) return null;
        var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value.StartsWith('h') ? amount * 60 : amount;
    }

    private static string Human(int minutes) => minutes switch
    {
        < 1 => "em menos de um minuto",
        1 => "em 1 minuto",
        < 60 => $"em {minutes} minutos",
        60 => "em 1 hora",
        _ => $"em {minutes / 60.0:0.#} horas",
    };

    /// <summary>Executa um comando já confirmado (ex.: desligar).</summary>
    public static OrbiReply Execute(string command)
    {
        var text = Normalize(command);
        var minutes = Minutes(text) ?? 0;

        if (Cancel().IsMatch(text))
        {
            SystemActions.CancelShutdown();
            return OrbiReply.Local("Desligamento cancelado. Seu PC segue como está.");
        }
        if (Shutdown().IsMatch(text))
        {
            return OrbiReply.Local(SystemActions.Shutdown(minutes));
        }
        if (Restart().IsMatch(text))
        {
            return OrbiReply.Local(SystemActions.Restart(minutes));
        }
        return Interpret(command);
    }

    public static OrbiReply Interpret(string input)
    {
        var text = Normalize(input);
        if (string.IsNullOrWhiteSpace(text)) return OrbiReply.Local("Me diga o que você precisa.");

        /* ----------------------------- desligar / reiniciar ----------------------------- */
        if (Cancel().IsMatch(text))
        {
            return OrbiReply.Local("Não há desligamento agendado. O PC segue como está.");
        }
        if (Shutdown().IsMatch(text))
        {
            var minutes = Minutes(text);
            var when = minutes is > 0 ? Human(minutes.Value) : "agora";
            return OrbiReply.Confirm(
                $"Desligar o PC {when}? Isso fecha tudo o que estiver aberto.",
                "Confirme para executar de verdade.");
        }
        if (Restart().IsMatch(text))
        {
            var minutes = Minutes(text);
            var when = minutes is > 0 ? Human(minutes.Value) : "agora";
            return OrbiReply.Confirm($"Reiniciar o PC {when}?", "Confirme para executar de verdade.");
        }
        if (SleepCmd().IsMatch(text))
        {
            return OrbiReply.Local(SystemActions.Sleep());
        }
        if (LockCmd().IsMatch(text))
        {
            return OrbiReply.Local(SystemActions.Lock(), detail: "Usa o bloqueio nativo do Windows.");
        }

        /* ----------------------------- lembretes ----------------------------- */
        if (Reminder().IsMatch(text))
        {
            var minutes = Minutes(text);
            var task = ReminderText().Replace(input.Trim(), "").Trim(' ', '.', ',', '!');
            if (minutes is null)
            {
                return OrbiReply.Local("Em quanto tempo eu te aviso? Diga, por exemplo: “me lembra em 20 min de beber água”.");
            }
            var reminder = new Reminder
            {
                Text = string.IsNullOrWhiteSpace(task) ? "Lembrete" : task,
                DueAt = DateTime.Now.AddMinutes(minutes.Value),
            };
            Store.Current.Reminders.Add(reminder);
            Store.Save();
            return OrbiReply.Local(
                $"Combinado. Te aviso {Human(minutes.Value)} sobre “{reminder.Text}”. Fica salvo neste PC.",
                $"Próximo aviso às {reminder.DueAt:HH:mm}.");
        }

        if (NotesCmd().IsMatch(text))
        {
            var note = NoteText().Replace(input.Trim(), "").Trim(' ', ':');
            if (string.IsNullOrWhiteSpace(note)) return OrbiReply.Local("O que você quer anotar?");
            Store.Current.Notes.Insert(0, new Note { Text = note });
            Store.Save();
            return OrbiReply.Local($"Anotado: “{note}”.", "Fica no seu PC, em Ajustes ▸ Notas.");
        }

        /* ----------------------------- notas e cópia ----------------------------- */
        if (RemindersList().IsMatch(text))
        {
            var pending = Store.Current.Reminders.Where(r => !r.Done && r.DueAt > DateTime.Now).ToList();
            if (pending.Count == 0) return OrbiReply.Local("Nenhum lembrete pendente.");
            var list = string.Join("\n", pending.Select(r => $"• {r.DueAt:HH:mm} — {r.Text}"));
            return OrbiReply.Local($"Você tem {pending.Count} lembrete(s):\n{list}");
        }

        if (ClipboardCmd().IsMatch(text))
        {
            try
            {
                var content = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(content)) return OrbiReply.Private("A área de transferência está vazia.");
                Store.PushClipboard(content);
                var preview = content.Length > 220 ? content[..220] + "…" : content;
                return OrbiReply.Private(preview, "Copiado para o histórico local.");
            }
            catch
            {
                return OrbiReply.Private("Não consegui ler a área de transferência agora.");
            }
        }

        if (CopyHistory().IsMatch(text))
        {
            var history = Store.Current.ClipboardHistory.Take(5).ToList();
            if (history.Count == 0) return OrbiReply.Private("O histórico está vazio. Copie algo primeiro.");
            var list = string.Join("\n", history.Select((item, index) =>
                $"{index + 1}. {(item.Length > 70 ? item[..70] + "…" : item)}"));
            return OrbiReply.Private(list, "Últimos itens copiados neste PC.");
        }

        if (ShortcutExpand().IsMatch(text))
        {
            var name = ShortcutName().Replace(text, "").Trim();
            var shortcut = Store.Current.Shortcuts.FirstOrDefault(
                item => item.Trigger.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (shortcut is null) return OrbiReply.Local($"Não tenho atalho “{name}”. Cadastre em Ajustes ▸ Atalhos.");
            try { System.Windows.Clipboard.SetText(shortcut.Expansion); } catch { }
            return OrbiReply.Local(shortcut.Expansion, "Copiado para a área de transferência.");
        }

        /* ----------------------------- volume / brilho ----------------------------- */
        if (VolumeCmd().IsMatch(text))
        {
            if (MuteCmd().IsMatch(text))
            {
                SystemActions.Mute();
                return OrbiReply.Local("Som silenciado.");
            }
            var number = Number().Match(text);
            if (number.Success && int.TryParse(number.Groups["n"].Value, out var level))
            {
                return OrbiReply.Local(SystemActions.SetVolume(level));
            }
            if (UpCmd().IsMatch(text))
            {
                SystemActions.VolumeUp(20);
                return OrbiReply.Local("Volume mais alto.");
            }
            if (DownCmd().IsMatch(text))
            {
                SystemActions.VolumeDown(20);
                return OrbiReply.Local("Volume mais baixo.");
            }
            return OrbiReply.Local("Diga o nível que você quer: “volume em 30”.");
        }

        if (BrightnessCmd().IsMatch(text))
        {
            var number = Number().Match(text);
            if (number.Success && int.TryParse(number.Groups["n"].Value, out var level))
            {
                return OrbiReply.Local(SystemActions.SetBrightness(level));
            }
            return OrbiReply.Local(SystemActions.BrightnessStep(UpCmd().IsMatch(text) ? 15 : -15));
        }

        /* ----------------------------- captura / sistema ----------------------------- */
        if (ScreenshotCmd().IsMatch(text)) return OrbiReply.Local(SystemActions.Screenshot());

        if (WifiCmd().IsMatch(text))
        {
            return OrbiReply.Local(SystemActions.Wifi(!OffCmd().IsMatch(text)), "Requer adaptador Wi-Fi.");
        }

        if (RecycleCmd().IsMatch(text)) return OrbiReply.Local(SystemActions.EmptyRecycleBin());

        if (StatusCmd().IsMatch(text))
        {
            var snapshot = SystemActions.Snapshot();
            var battery = snapshot.Battery is { } level ? $" Bateria em {level}%." : "";
            var top = snapshot.TopProcess is { } process ? $" Quem mais usa memória agora é {process}." : "";
            return OrbiReply.Local(
                $"CPU em {snapshot.Cpu:0}%, {snapshot.MemoryUsedGb:0.#} de {snapshot.MemoryTotalGb:0.#} GB de memória " +
                $"e {100 * snapshot.DiskFreeGb / Math.Max(1, snapshot.DiskTotalGb):0}% do disco livre.{battery}{top}",
                "Lido em tempo real no seu Windows.");
        }

        if (MediaCmd().IsMatch(text))
        {
            SystemActions.PlayPause();
            return OrbiReply.Local("Play/pause enviado para o player.");
        }

        /* ----------------------------- rotinas ----------------------------- */
        // As rotinas vêm da conta (Firestore) e são editáveis no site.
        var routine = Store.Current.Routines.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.Name) &&
            (text == Normalize(item.Name) || text.Contains(Normalize(item.Name), StringComparison.Ordinal)));

        if (routine is not null)
        {
            var results = new List<string>();
            foreach (var command in routine.Commands.Take(12))
            {
                var step = Interpret(command);
                // Dentro de uma rotina não pedimos confirmação: pulamos ações destrutivas.
                if (step.Kind == ReplyKind.Confirm)
                {
                    results.Add($"(pulado: {command})");
                    continue;
                }
                results.Add(step.Text);
            }
            return OrbiReply.Local(
                $"Rotina “{routine.Name}” executada.",
                string.Join(" ", results));
        }

        if (RoutineListCmd().IsMatch(text))
        {
            var names = Store.Current.Routines.Select(item => item.Name).Where(name => name.Length > 0).ToList();
            return names.Count == 0
                ? OrbiReply.Local("Você ainda não tem rotinas. Crie uma no site, em Conta ▸ Sincronização ▸ Rotinas.")
                : OrbiReply.Local($"Suas rotinas: {string.Join(", ", names)}.", "Diga o nome de uma para executar.");
        }

        /* ----------------------------- abrir / fechar apps ----------------------------- */
        var openMatch = OpenCmd().Match(text);
        if (openMatch.Success)
        {
            var target = openMatch.Groups["app"].Value.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                return OrbiReply.Local("Qual app devo abrir?");
            }
            return OrbiReply.Local(SystemActions.Open(target), "Busca por apelido no seu PC.");
        }

        var closeMatch = CloseCmd().Match(text);
        if (closeMatch.Success)
        {
            var target = closeMatch.Groups["app"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(target))
            {
                return OrbiReply.Confirm($"Fechar {target}? Algo não salvo pode se perder.");
            }
        }

        /* ----------------------------- ajuda ----------------------------- */
        if (Greeting().IsMatch(text))
        {
            var name = string.IsNullOrWhiteSpace(Store.Current.UserName) ? "" : $", {Store.Current.UserName.Split(' ')[0]}";
            return OrbiReply.Local(
                $"Oi{name}. Estou aqui. Posso abrir apps, ajustar volume e brilho, criar lembretes, bloquear o PC e ler o status do sistema.",
                "Alt+Space abre esta barra em qualquer programa.");
        }

        if (Thanks().IsMatch(text)) return OrbiReply.Local("De nada. Alt+Space quando precisar.");

        if (HelpCmd().IsMatch(text))
        {
            return OrbiReply.Local(
                "Experimente: “abre o zap”, “volume em 30”, “me lembra em 20 min de beber água”, " +
                "“status do PC”, “captura a tela”, “bloqueia o PC”, “desliga em 1 hora”.",
                "Tudo isso funciona sem internet.");
        }

        /* ----------------------------- nada reconhecido ----------------------------- */
        return OrbiReply.Ai(input);
    }

    /* ----------------------------- padrões ----------------------------- */
    [GeneratedRegex(@"\b(desliga|desligar|encerra|encerrar|shutdown)\b")]
    private static partial Regex Shutdown();

    [GeneratedRegex(@"\b(reinicia|reiniciar|restart|reboot)\b")]
    private static partial Regex Restart();

    [GeneratedRegex(@"\b(cancela|cancelar|cancela o desligamento|aborta)\b")]
    private static partial Regex Cancel();

    [GeneratedRegex(@"\b(dormir|suspender|hibernar|sleep)\b")]
    private static partial Regex SleepCmd();

    [GeneratedRegex(@"\b(bloqueia|bloquear|tranca|trancar|lock)\b")]
    private static partial Regex LockCmd();

    [GeneratedRegex(@"\b(lembra|lembre|lembrete|me avisa|avisa|alarme)\b")]
    private static partial Regex Reminder();

    [GeneratedRegex(@"^.*?(lembra[r]?|lembrete|me avisa[r]?|alarme)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ReminderText();

    [GeneratedRegex(@"\b(anota|anote|nota|anotar)\b")]
    private static partial Regex NotesCmd();

    [GeneratedRegex(@"^.*?(anota[r]?|anote|nota)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex NoteText();

    [GeneratedRegex(@"\b(meus lembretes|listar lembretes|lembretes pendentes)\b")]
    private static partial Regex RemindersList();

    [GeneratedRegex(@"\b(o que eu copiei|copia o que eu copiei|area de transferencia)\b")]
    private static partial Regex ClipboardCmd();

    [GeneratedRegex(@"\b(historico de copia|ultimos copiados)\b")]
    private static partial Regex CopyHistory();

    [GeneratedRegex(@"\b(expande|expandir|atalho)\b")]
    private static partial Regex ShortcutExpand();

    [GeneratedRegex(@"^.*?(expande|expandir|atalho)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ShortcutName();

    [GeneratedRegex(@"\b(volume|som|audio)\b")]
    private static partial Regex VolumeCmd();

    [GeneratedRegex(@"\b(mudo|muta|mudo|silencio|silenciar)\b")]
    private static partial Regex MuteCmd();

    [GeneratedRegex(@"\b(mais alto|aumenta|aumentar|sobe)\b")]
    private static partial Regex UpCmd();

    [GeneratedRegex(@"\b(mais baixo|diminui|diminuir|desce|abaixa)\b")]
    private static partial Regex DownCmd();

    [GeneratedRegex(@"\b(brilho|brilhar|tela mais clara|tela mais escura)\b")]
    private static partial Regex BrightnessCmd();

    [GeneratedRegex(@"\b(captura|print|screenshot|foto da tela)\b")]
    private static partial Regex ScreenshotCmd();

    [GeneratedRegex(@"\b(wifi|wi-fi|wireless)\b")]
    private static partial Regex WifiCmd();

    [GeneratedRegex(@"\b(desliga o wifi|desligar o wifi|sem wifi|wifi off|desativa o wifi)\b")]
    private static partial Regex OffCmd();

    [GeneratedRegex(@"\b(lixeira)\b")]
    private static partial Regex RecycleCmd();

    [GeneratedRegex(@"\b(minhas rotinas|listar rotinas|quais rotinas)\b")]
    private static partial Regex RoutineListCmd();

    [GeneratedRegex(@"\b(status|como esta o pc|cpu|memoria|ram|disco|bateria|desempenho)\b")]
    private static partial Regex StatusCmd();

    [GeneratedRegex(@"\b(play|pausa|pause|toca|proxima|proximo)\b")]
    private static partial Regex MediaCmd();

    [GeneratedRegex(@"\b(abre|abrir|abre o|inicia|iniciar|executa|executar|lanca|traz)\b\s*(?<app>[\w\s\.\-:]{2,40})?")]
    private static partial Regex OpenCmd();

    [GeneratedRegex(@"\b(fecha|fechar|encerra|matar)\b\s*(?<app>[\w\s\.\-]{2,40})?")]
    private static partial Regex CloseCmd();

    [GeneratedRegex(@"\b(oi|ola|eai|e ai|bom dia|boa tarde|boa noite|tudo bem|opa)\b")]
    private static partial Regex Greeting();

    [GeneratedRegex(@"\b(obrigad|valeu|brigad)\b")]
    private static partial Regex Thanks();

    [GeneratedRegex(@"\b(ajuda|help|o que voce faz|comandos)\b")]
    private static partial Regex HelpCmd();
}
