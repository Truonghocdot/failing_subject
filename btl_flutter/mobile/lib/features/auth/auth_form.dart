import 'package:flutter/material.dart';

/// Kiểm tra tại chỗ – khớp quy tắc backend (RequestValidator).
abstract final class AuthValidators {
  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  static String? email(String? value) => _email.hasMatch(value?.trim() ?? '') ? null : 'Email không hợp lệ';

  static String? password(String? value) => (value ?? '').length < 8 ? 'Mật khẩu tối thiểu 8 ký tự' : null;

  static String? displayName(String? value) {
    final name = value?.trim() ?? '';
    if (name.isEmpty) return 'Vui lòng nhập tên hiển thị';
    return name.length > 100 ? 'Tên hiển thị tối đa 100 ký tự' : null;
  }
}

/// Khung chung cho màn đăng nhập / đăng ký: tiêu đề, các trường, lỗi, nút gửi, liên kết phụ.
class AuthFormScaffold extends StatelessWidget {
  const AuthFormScaffold({
    super.key,
    required this.formKey,
    required this.title,
    required this.subtitle,
    required this.fields,
    required this.submitLabel,
    required this.submitting,
    required this.onSubmit,
    required this.footer,
    this.error,
    this.validateWhileTyping = false,
  });

  /// Bật sau lần bấm gửi đầu tiên → lỗi của từng ô tự cập nhật khi người dùng sửa.
  final bool validateWhileTyping;

  final GlobalKey<FormState> formKey;
  final String title;
  final String subtitle;
  final List<Widget> fields;
  final String submitLabel;
  final bool submitting;
  final VoidCallback onSubmit;
  final Widget footer;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: formKey,
                autovalidateMode: validateWhileTyping ? AutovalidateMode.always : AutovalidateMode.disabled,
                child: AutofillGroup(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Text(title, style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold)),
                      const SizedBox(height: 6),
                      Text(subtitle, style: TextStyle(color: theme.colorScheme.onSurfaceVariant)),
                      const SizedBox(height: 24),
                      for (final field in fields) Padding(padding: const EdgeInsets.only(bottom: 14), child: field),
                      if (error != null)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 14),
                          child: Text(error!, style: TextStyle(color: theme.colorScheme.error)),
                        ),
                      SizedBox(
                        height: 48,
                        child: FilledButton(
                          onPressed: submitting ? null : onSubmit,
                          child: submitting
                              ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2.5))
                              : Text(submitLabel),
                        ),
                      ),
                      const SizedBox(height: 12),
                      footer,
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Ô mật khẩu có nút ẩn/hiện.
class PasswordField extends StatefulWidget {
  const PasswordField({
    super.key,
    required this.controller,
    this.label = 'Mật khẩu',
    this.validator,
    this.autofillHints = const [AutofillHints.password],
    this.textInputAction = TextInputAction.done,
    this.onSubmitted,
  });

  final TextEditingController controller;
  final String label;
  final FormFieldValidator<String>? validator;
  final Iterable<String> autofillHints;
  final TextInputAction textInputAction;
  final ValueChanged<String>? onSubmitted;

  @override
  State<PasswordField> createState() => _PasswordFieldState();
}

class _PasswordFieldState extends State<PasswordField> {
  bool _obscure = true;

  // Bỏ nút ẩn/hiện khỏi thứ tự focus: "Tiếp" trên bàn phím phải nhảy sang ô kế, không vào nút này
  final _toggleFocus = FocusNode(skipTraversal: true);

  @override
  void dispose() {
    _toggleFocus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: widget.controller,
      obscureText: _obscure,
      autofillHints: widget.autofillHints,
      textInputAction: widget.textInputAction,
      onFieldSubmitted: widget.onSubmitted,
      validator: widget.validator,
      decoration: InputDecoration(
        labelText: widget.label,
        border: const OutlineInputBorder(),
        suffixIcon: IconButton(
          focusNode: _toggleFocus,
          tooltip: _obscure ? 'Hiện mật khẩu' : 'Ẩn mật khẩu',
          onPressed: () => setState(() => _obscure = !_obscure),
          icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
        ),
      ),
    );
  }
}
