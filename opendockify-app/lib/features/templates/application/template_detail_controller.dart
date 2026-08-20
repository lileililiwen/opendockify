import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';

/// Loads a single template by id.
class TemplateDetailController extends AsyncNotifier<Template> {
  TemplateDetailController(this.templateId);

  final String templateId;

  @override
  Future<Template> build() {
    return ref.read(apiClientProvider).getTemplate(templateId);
  }
}

final templateDetailControllerProvider =
    AsyncNotifierProvider.family<TemplateDetailController, Template, String>(TemplateDetailController.new);