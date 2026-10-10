from contextlib import ExitStack, redirect_stderr, redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import content_report
import verify


def write_trx(directory, total=2, executed=1, passed=1, failed=0):
    path = directory / "tests.trx"
    path.write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
      <Results><UnitTestResult testName="Example" outcome="{'Failed' if failed else 'Passed'}"><Output><ErrorInfo>
      <Message>wrong state</Message><StackTrace>Example.cs:42</StackTrace>
      </ErrorInfo></Output></UnitTestResult></Results>
      <ResultSummary><Counters total="{total}" executed="{executed}" passed="{passed}"
      failed="{failed}" notExecuted="{total-executed}" /></ResultSummary></TestRun>''', encoding="utf-8")
    return path


class VerificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="opensage-verification-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.project = self.root / verify.TARGETS["quick"]
        self.project.parent.mkdir(parents=True)
        self.project.write_text('<Project Sdk="Microsoft.NET.Sdk" />', encoding="utf-8")
        self.assets = self.project.parent / "obj/project.assets.json"
        self.assets.parent.mkdir()
        self.assets.write_text("{}", encoding="utf-8")
        self.runner = verify.Verifier(self.root, verify.parse_args(["quick"]))
        self.commands = []
        self.stdout = io.StringIO()
        stack = ExitStack()
        self.addCleanup(stack.close)
        stack.enter_context(redirect_stdout(self.stdout))
        stack.enter_context(redirect_stderr(self.stdout))

    def fake_command(self, name, command, check=True):
        self.commands.append((name, command))
        if name == "sdk":
            (self.runner.directory / "sdk.log").write_text("8.0.100\n", encoding="utf-8")
        if name == "tests":
            write_trx(self.runner.directory)
        return 0

    def test_build_failure_stops_before_testing_and_writes_failure_report(self):
        def command(name, args, check=True):
            if name == "build":
                raise verify.VerificationFailure("build failed")
            return self.fake_command(name, args, check)
        self.runner.command = command
        self.assertEqual(1, self.runner.execute())
        self.assertNotIn("tests", [name for name, _ in self.commands])
        report = json.loads((self.runner.directory / "report.json").read_text())
        self.assertEqual("failed", report["status"])
        self.assertFalse(report["completionGatePassed"])

    def test_runner_builds_before_tests_and_never_rebuilds_inside_test_command(self):
        self.runner.command = self.fake_command
        self.runner.options.filter = "FullyQualifiedName~Physics|FullyQualifiedName~Damage"
        self.assertEqual(0, self.runner.execute())
        names = [name for name, _ in self.commands]
        self.assertLess(names.index("build"), names.index("tests"))
        command = dict(self.commands)["tests"]
        self.assertIn("--no-build", command)
        self.assertIn("--no-restore", command)
        self.assertEqual("Category!=Interactive&(FullyQualifiedName~Physics|FullyQualifiedName~Damage)",
                         command[command.index("--filter") + 1])
        self.assertEqual(1, self.runner.report["tests"]["counts"]["skipped"])

    def test_missing_assets_are_skipped_without_running_build_or_restore(self):
        self.runner.options.mode = "assets"
        self.runner.command = self.fake_command
        with patch.dict(verify.os.environ, {"CNC_GENERALS_PATH": "", "CNC_GENERALS_ZH_PATH": ""}):
            self.assertEqual(2, self.runner.execute())
        self.assertEqual([], self.commands)
        self.assertEqual("skipped", self.runner.report["status"])
        self.assertFalse(self.runner.report["completionGatePassed"])

    def test_restore_cache_invalidates_for_changed_inputs_or_deleted_assets(self):
        projects = verify.project_graph(self.root, self.project)
        stamp = self.root / "cache.json"
        fingerprint = verify.restore_inputs(self.root, "8.0.100", "Debug", False)
        verify.write_json(stamp, {"inputs": fingerprint, "assets": verify.assets_hashes(projects)})
        self.assertTrue(verify.restore_is_current(stamp, fingerprint, projects))
        packages = self.root / "src/Directory.Packages.props"
        packages.write_text("changed packages", encoding="utf-8")
        changed = verify.restore_inputs(self.root, "8.0.100", "Debug", False)
        self.assertFalse(verify.restore_is_current(stamp, changed, projects))
        self.assets.unlink()
        self.assertFalse(verify.restore_is_current(stamp, fingerprint, projects))

    def test_inventory_timeout_fails_the_run_without_passing_through_step_exit_code(self):
        self.runner.options.mode = "assets"
        self.runner.asset_paths = lambda: [str(self.root), str(self.root)]
        self.runner.restore = lambda target: None
        def command(name, args, check=True):
            if name == "inventory":
                return 124
            return self.fake_command(name, args, check)
        self.runner.command = command
        self.assertEqual(1, self.runner.execute())
        self.assertEqual("failed", self.runner.report["status"])

    def test_external_restore_changes_invalidate_cached_assets(self):
        stamp = self.root / "cache.json"
        verify.write_json(stamp, {"inputs": "same", "assets": verify.assets_hashes([self.project])})
        self.assets.write_text('{"changed":true}', encoding="utf-8")
        self.assertFalse(verify.restore_is_current(stamp, "same", [self.project]))

    def test_zero_executed_tests_cannot_pass(self):
        write_trx(self.root, total=0, executed=0, passed=0)
        with self.assertRaisesRegex(ValueError, "Zero tests"):
            verify.read_test_results(self.root)

    def test_failure_message_and_stack_are_preserved_from_trx(self):
        write_trx(self.root, total=2, executed=1, passed=0, failed=1)
        result = verify.read_test_results(self.root)
        self.assertEqual(1, result["counts"]["failed"])
        self.assertEqual("wrong state", result["failures"][0]["message"])
        self.assertEqual("Example.cs:42", result["failures"][0]["stackTrace"])

    def test_missing_trx_is_a_failure(self):
        with self.assertRaisesRegex(ValueError, "No TRX"):
            verify.read_test_results(self.root)

    def test_skipped_result_is_visible_when_adapter_counter_is_zero(self):
        path = write_trx(self.root, total=2, executed=1, passed=1)
        tree = verify.ET.parse(path)
        tree.find(".//{*}Counters").set("notExecuted", "0")
        results = tree.find(".//{*}Results")
        verify.ET.SubElement(results, "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult",
                             {"testName": "SkippedExample", "outcome": "NotExecuted"})
        tree.write(path)
        result = verify.read_test_results(self.root)
        self.assertEqual(1, result["counts"]["skipped"])
        self.assertEqual("SkippedExample", result["skipped"][0]["test"])

    def test_lock_prevents_overlapping_runs_and_releases_after_failure(self):
        lock = self.root / "lock"
        with self.assertRaisesRegex(ValueError, "simulated"):
            with verify.verification_lock(lock):
                with self.assertRaisesRegex(RuntimeError, "Another verification"):
                    with verify.verification_lock(lock):
                        self.fail("Concurrent lock acquired")
                raise ValueError("simulated")
        self.assertFalse(lock.exists())
        with verify.verification_lock(lock):
            self.assertTrue(lock.exists())

    def test_timeout_stops_command_and_records_failure(self):
        self.runner.directory.mkdir(parents=True)
        self.runner.options.timeout = 0.1
        code = self.runner.command("timeout", [sys.executable, "-c", "import time; time.sleep(30)"], check=False)
        self.assertEqual(124, code)
        self.assertTrue(self.runner.report["steps"][-1]["timedOut"])


class ContentQueryTests(unittest.TestCase):
    def setUp(self):
        self.manifests = [{
            "FormatVersion": 1, "Game": "CncGenerals", "Failures": [
                {"Source": "USA/Mission01", "Feature": "Movie:Intro", "Message": "missing"}],
            "Maps": [{"Path": "Maps/USA01/USA01.map", "ScriptFeatures": [
                {"Script": "Player/Intro", "Feature": "MoviePlay", "Kind": "Action", "Status": "MissingHandler"},
                {"Script": "Player/Timer", "Feature": "TimerSet", "Kind": "Action", "Status": "RegisteredUnverified"},
                {"Script": "Player/Intro2", "Feature": "MoviePlay", "Kind": "Action", "Status": "MissingHandler"}]}],
        }]

    def test_query_preserves_origin_and_limits_output(self):
        result = content_report.query(self.manifests, feature="movie", map_name="usa01", limit=1)
        self.assertEqual(2, result["matchingRows"])
        self.assertTrue(result["truncated"])
        self.assertEqual("Player/Intro", result["results"][0]["Script"])
        self.assertFalse(result["completionGatePassed"])

    def test_summary_groups_missing_usages_without_counting_registered_as_supported(self):
        result = content_report.query(self.manifests)
        self.assertEqual(1, result["matchingRows"])
        self.assertEqual(2, result["results"][0]["usages"])
        self.assertEqual(1, result["results"][0]["maps"])
        registered = content_report.query(self.manifests, status="RegisteredUnverified")
        self.assertEqual("TimerSet", registered["results"][0]["feature"])

    def test_file_failures_can_be_queried_separately(self):
        result = content_report.query(self.manifests, feature="movie", failures=True)
        self.assertEqual("USA/Mission01", result["results"][0]["Source"])

    def test_summary_examples_show_distinct_origins_instead_of_duplicate_actions(self):
        usages = self.manifests[0]["Maps"][0]["ScriptFeatures"]
        usages.append(dict(usages[0]))
        result = content_report.query(self.manifests)
        self.assertEqual(3, result["results"][0]["usages"])
        self.assertEqual(2, len(result["results"][0]["examples"]))

    def test_unknown_format_is_rejected(self):
        self.manifests[0]["FormatVersion"] = 99
        with self.assertRaisesRegex(ValueError, "format version"):
            content_report.query(self.manifests)


if __name__ == "__main__":
    unittest.main()
