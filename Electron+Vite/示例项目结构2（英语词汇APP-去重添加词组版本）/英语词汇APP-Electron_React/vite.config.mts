import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

// 只在生产构建注入严格 CSP：dev 下 @vitejs/plugin-react 要注入内联 preamble，严格策略会挡住 HMR。
function prodCsp(): Plugin {
  return {
    name: 'prod-csp',
    apply: 'build',
    transformIndexHtml: () => [
      {
        tag: 'meta',
        attrs: {
          'http-equiv': 'Content-Security-Policy',
          content: [
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data:",
            "font-src 'self' data:",
            "connect-src 'self'",
            "object-src 'none'",
            "base-uri 'none'",
            "form-action 'none'"
          ].join('; ')
        },
        injectTo: 'head-prepend'
      }
    ]
  };
}

export default defineConfig({
  plugins: [react(), prodCsp()],
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    target: 'es2022',
    sourcemap: false
  },
  server: {
    strictPort: true
  }
});
