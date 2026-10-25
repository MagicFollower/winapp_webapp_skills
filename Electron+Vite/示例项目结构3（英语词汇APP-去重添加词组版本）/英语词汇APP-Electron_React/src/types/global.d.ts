import type { Api } from '../../electron/api-contract';

declare global {
  interface Window {
    api: Api;
    __rendererErrors: string[];
  }
}

export {};
