import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import 'auth_form.dart';
import 'auth_providers.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _submitting = false;
  bool _attempted = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_attempted) setState(() => _attempted = true);
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref.read(authProvider.notifier).login(email: _email.text, password: _password.text);
      if (mounted) closeAuthScreen(context);
    } catch (e) {
      if (mounted) setState(() => _error = errorMessage(e));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthFormScaffold(
      formKey: _formKey,
      title: 'Đăng nhập',
      subtitle: 'Lưu phim yêu thích và xem tiếp trên mọi thiết bị',
      error: _error,
      validateWhileTyping: _attempted,
      submitting: _submitting,
      submitLabel: 'Đăng nhập',
      onSubmit: _submit,
      fields: [
        TextFormField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.email],
          textInputAction: TextInputAction.next,
          validator: AuthValidators.email,
          decoration: const InputDecoration(labelText: 'Email', border: OutlineInputBorder()),
        ),
        PasswordField(
          controller: _password,
          validator: (v) => (v ?? '').isEmpty ? 'Vui lòng nhập mật khẩu' : null,
          onSubmitted: (_) => _submit(),
        ),
      ],
      footer: TextButton(
        onPressed: _submitting ? null : () => context.pushReplacement(Routes.register),
        child: const Text('Chưa có tài khoản? Đăng ký'),
      ),
    );
  }
}

/// Đăng nhập/đăng ký xong → quay về màn hình trước (VD trang chi tiết vừa bấm ♥).
void closeAuthScreen(BuildContext context) {
  if (context.canPop()) {
    context.pop(true);
  } else {
    context.go(Routes.library);
  }
}
