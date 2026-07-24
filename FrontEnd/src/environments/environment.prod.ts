import packageInfo from '../../package.json';

export const environment = {
  appVersion: packageInfo.version,
  production: true,
  apiBaseUrl: 'https://api.leads.grandessites.com.br',
  googleClientId: ''
};
