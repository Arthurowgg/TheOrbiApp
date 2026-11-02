/**
 * Assistente Orbi — Gemini via Firebase AI Logic.
 *
 * Por que Firebase AI Logic: a chamada sai do navegador para o proxy do
 * Firebase, que fala com o Gemini Developer API (camada gratuita, Spark).
 * Nenhuma chave do Gemini fica no código do site — só a config pública do
 * projeto, protegida pelas Security Rules e pelo App Check.
 *
 * Se a IA ainda não estiver habilitada no console (ou a rede cair), o
 * assistente responde com o motor de regras local, que entende os comandos
 * do app sem precisar de internet. Nunca deixamos a pessoa sem resposta.
 */
import { getAI, getGenerativeModel, GoogleAIBackend } from "firebase/ai";
import { app } from "./firebaseClient";
import { GEMINI_MODEL } from "./firebase";

export type AiStatus = "unknown" | "online" | "offline";

let status: AiStatus = "unknown";
const listeners = new Set<(status: AiStatus) => void>();

export function onAiStatus(listener: (status: AiStatus) => void) {
  listeners.add(listener);
  listener(status);
  return () => {
    listeners.delete(listener);
  };
}

function setStatus(next: AiStatus) {
  if (status === next) return;
  status = next;
  listeners.forEach((listener) => listener(next));
}

let model: ReturnType<typeof getGenerativeModel> | null = null;

function getModel() {
  if (model) return model;
  const ai = getAI(app, { backend: new GoogleAIBackend() });
  model = getGenerativeModel(ai, {
    model: GEMINI_MODEL,
    systemInstruction: `Você é o Orbi, assistente do app Orbi para Windows 11.
Fala português do Brasil, direto e sem floreio. Responde em no máximo 3 frases curtas.
Quando a pessoa pede algo que o app faz (abrir programas, volume, brilho, lembretes,
timers, rotinas, bloqueio do PC, status do sistema), diga exatamente qual comando
ela deve escrever na barra de comando do app — use a forma mais curta possível.
Nunca prometa ações que só acontecem no app do PC: aqui no site você apenas explica.
Se não souber, diga em uma frase.`,
  });
  return model;
}

const HISTORY_LIMIT = 8;

/**
 * Pergunta ao Gemini. Devolve `null` quando a IA não está disponível —
 * quem chamou decide o fallback.
 */
export async function askGemini(
  prompt: string,
  history: { role: "user" | "model"; text: string }[] = [],
): Promise<string | null> {
  try {
    const chat = getModel().startChat({
      history: history.slice(-HISTORY_LIMIT).map((item) => ({
        role: item.role,
        parts: [{ type: "text" as const, text: item.text }],
      })),
      generationConfig: { temperature: 0.6, maxOutputTokens: 320 },
    });
    const result = await chat.sendMessage(prompt);
    const text = result.response.text()?.trim();
    if (!text) throw new Error("resposta vazia");
    setStatus("online");
    return text;
  } catch (error) {
    const code = (error as { code?: string })?.code ?? "";
    // "no-api-key" / "fetch-error" = IA ainda não habilitada no console.
    if (import.meta.env.DEV) {
      console.warn("[Orbi] Gemini indisponível:", code || error);
    }
    setStatus("offline");
    return null;
  }
}

export function aiLabel(status: AiStatus) {
  if (status === "online") return "Gemini online";
  if (status === "offline") return "modo local";
  return "conectando";
}
