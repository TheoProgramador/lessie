// This file can be replaced during build by using the `fileReplacements` array.
// `ng build` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

import packageInfo from '../../package.json';

export const environment = {
  appVersion: packageInfo.version,
  production: false,
  apiBaseUrl: 'http://wlsolutions-002-site10.gtempurl.com',
  googleClientId: '465394213386-0kqcqu1ialvno3st14ifd8h82fc4ga23.apps.googleusercontent.com',
  devAdminAccessKey: 'LessieDevAdminKey_2026!'
};
/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
