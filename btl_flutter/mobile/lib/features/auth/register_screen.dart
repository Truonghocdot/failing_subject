import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import 'auth_form.dart';
import 'auth_providers.dart';
import 'login_screen.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  bool _submitting = false;
  bool _attempted = false;
  String? _error;

  @override
  void dispose() {
    for (final controller in [_name, _email, _password, _confirm]) {
      controller.dispose();
    }
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
      await ref
          .read(authProvider.notifier)
          .register(email: _email.text, password: _password.text, displayName: _name.text);
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
      title: 'Tạo tài khoản',
      subtitle: 'Miễn phí – chỉ cần email và mật khẩu',
      error: _error,
      validateWhileTyping: _attempted,
      submitting: _submitting,
      submitLabel: 'Đăng ký',
      onSubmit: _submit,
      fields: [
        TextFormField(
          controller: _name,
          autofillHints: const [AutofillHints.name],
          textInputAction: TextInputAction.next,
          validator: AuthValidators.displayName,
          decoration: const InputDecoration(labelText: 'Tên hiển thị', border: OutlineInputBorder()),
        ),
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
          autofillHints: const [AutofillHints.newPassword],
          textInputAction: TextInputAction.next,
          validator: AuthValidators.password,
        ),
        PasswordField(
          controller: _confirm,
          label: 'Nhập lại mật khẩu',
          autofillHints: const [AutofillHints.newPassword],
          validator: (v) => v == _password.text ? null : 'Mật khẩu nhập lại không khớp',
          onSubmitted: (_) => _submit(),
        ),
      ],
      footer: TextButton(
        onPressed: _submitting ? null : () => context.pushReplacement(Routes.login),
        child: const Text('Đã có tài khoản? Đăng nhập'),
      ),
    );
  }
}
