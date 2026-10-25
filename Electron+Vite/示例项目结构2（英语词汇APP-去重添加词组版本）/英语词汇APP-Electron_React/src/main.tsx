import { createRoot } from 'react-dom/client';
import App from './components/App';
import './styles/tokens.css';
import './styles/app.css';

const sink: string[] = [];
window.__rendererErrors = sink;
window.addEventListener('error', (e) => sink.push(`error: ${e.message} @ ${e.filename}:${e.lineno}`));
window.addEventListener('unhandledrejection', (e) => sink.push(`rejection: ${String((e.reason as Error)?.message ?? e.reason)}`));

const host = document.getElementById('root');
if (!host) throw new Error('missing #root');

createRoot(host).render(<App />);
