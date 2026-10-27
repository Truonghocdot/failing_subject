/// Cấu hình nạp từ `mobile/.env` qua `flutter run --dart-define-from-file=.env`.
/// Giá trị bị nhúng vào APK – không đặt secret.
class Env {
  const Env._();

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5080',
  );

  static const apiTimeoutSeconds = int.fromEnvironment(
    'API_TIMEOUT_SECONDS',
    defaultValue: 15,
  );

  static const playerAllowedHostSuffix = String.fromEnvironment(
    'PLAYER_ALLOWED_HOST_SUFFIX',
    defaultValue: 'streamvsmov.com',
  );
}
