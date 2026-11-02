/**
 * Configuração pública do Firebase.
 *
 * A `apiKey` do Firebase é um identificador público do projeto — ela não dá
 * acesso a nada sem passar pelas Security Rules. O que protege os dados é:
 *   1. Authentication (cada pessoa só lê o próprio documento);
 *   2. Firestore Rules (ver firestore.rules);
 *   3. Firebase AI Logic (o Gemini é chamado pelo proxy do Firebase, então
 *      nenhuma chave de API do Gemini fica exposta no navegador).
 */
export const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? "AIzaSyDCpoYpdBiu_ENT8xFbvz0y1gl0W1CufFw",
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? "orbi-app-auth.firebaseapp.com",
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? "orbi-app-auth",
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET ?? "orbi-app-auth.firebasestorage.app",
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? "96738360575",
  appId: import.meta.env.VITE_FIREBASE_APP_ID ?? "1:96738360575:web:2bb25abca7d0bdde9220ca",
  measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID ?? "G-7CT3TDG2L7",
};

/** Modelo usado pelo assistente no site. Trocável sem mexer no código. */
export const GEMINI_MODEL = import.meta.env.VITE_GEMINI_MODEL ?? "gemini-2.5-flash";

/** Página de releases do app. O build de CI publica o instalador aqui. */
export const DOWNLOAD_BASE =
  import.meta.env.VITE_DOWNLOAD_URL ?? "https://github.com/Arthurowgg/TheOrbiApp/releases/latest";

export const SITE_URL = "https://orbi-app-auth.web.app";
