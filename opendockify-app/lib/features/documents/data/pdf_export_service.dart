import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../../../core/api/api_client.dart';
import '../../../core/providers.dart';

/// Downloads generated PDFs and opens/shares them via the platform.
class PdfExportService {
  PdfExportService(this._api);

  final ApiClient _api;

  Future<String> download(String downloadUrl, String documentId) async {
    final dir = await getTemporaryDirectory();
    final path = '${dir.path}/opendockify_$documentId.pdf';
    return _api.downloadPdf(downloadUrl, path);
  }

  Future<String> open(String path) async {
    final result = await OpenFilex.open(path);
    if (result.type != ResultType.done) {
      throw Exception('Could not open the PDF (${result.message}).');
    }
    return path;
  }

  Future<void> share(String path) async {
    await SharePlus.instance.share(
      ShareParams(
        files: [XFile(path, mimeType: 'application/pdf')],
        subject: 'OpenDockify document',
        title: 'OpenDockify document',
      ),
    );
  }
}

final pdfExportServiceProvider = Provider<PdfExportService>((ref) {
  return PdfExportService(ref.watch(apiClientProvider));
});