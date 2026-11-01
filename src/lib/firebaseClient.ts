/**
 * Inicializa uma única instância do Firebase para o site inteiro.
 * Analytics só roda quando existe `measurementId` e o domínio é https.
 */
import { initializeApp, type FirebaseApp } from "firebase/app";
import { getAuth, setPersistence, browserLocalPersistence, type Auth } from "firebase/auth";
import { getFirestore, type Firestore } from "firebase/firestore";
import { firebaseConfig } from "./firebase";

export const configured = Boolean(firebaseConfig.apiKey && firebaseConfig.projectId);

export const app: FirebaseApp = initializeApp(firebaseConfig);
export const auth: Auth = getAuth(app);
export const db: Firestore = getFirestore(app);

// Mantém a sessão entre visitas (o padrão do Firebase já é local, mas
// deixamos explícito para não depender de padrões).
setPersistence(auth, browserLocalPersistence).catch(() => undefined);

/** Carrega o Analytics sob demanda — não atrapalha o primeiro paint. */
export function initAnalytics() {
  if (!firebaseConfig.measurementId) return;
  if (!/^https?:$/.test(window.location.protocol)) return;
  import("firebase/analytics")
    .then(({ getAnalytics, isSupported }) =>
      isSupported().then((ok) => {
        if (ok) {
          const analytics = getAnalytics(app);
          void analytics;
        }
      }),
    )
    .catch(() => undefined);
}
