import copy
import json
import pathlib
import re
import subprocess
import sys
import tempfile
import unittest
from sql_matrix_expectations import load_expectations, assert_calls, assert_compiled_cases, assert_database_path_evidence

ROOT = pathlib.Path(__file__).resolve().parents[2]
class SqlOracleGuards(unittest.TestCase):
    def test_full_suite_ci_installs_and_checks_fixture_sdk(self):
        metadata=json.loads((ROOT/'samples/sql-database-matrix/global.json').read_text())
        self.assertIsInstance(metadata,dict)
        sdk=metadata.get('sdk')
        self.assertIsInstance(sdk,dict)
        self.assertIsInstance(sdk.get('version'),str)
        self.assertIsInstance(sdk.get('rollForward'),str)
        self.assertEqual('disable',sdk['rollForward'])
        workflow=(ROOT/'.github/workflows/adapter-validation.yml').read_text()
        dotnet_job=workflow.split('  dotnet:',1)[1].split('  typescript:',1)[0]
        setup=re.search(r'(?m)^      - uses: actions/setup-dotnet@[^\n]+\n(?:(?!      - ).*\n)*',dotnet_job)
        self.assertIsNotNone(setup)
        self.assertIn('            '+sdk['version']+'\n',setup.group())
        self.assertIn('working-directory: samples/sql-database-matrix\n        run: dotnet --version',dotnet_job)
        self.assertIn('dotnet test src/dotnet/TraceMap.sln --no-build',dotnet_job)
        self.assertLess(dotnet_job.index('run: dotnet --version'),dotnet_job.index('dotnet build'))

    def test_optimized_validation_refuses_before_scanning(self):
        result=subprocess.run([sys.executable,'-O',str(ROOT/'scripts/validation/sql-coverage-smoke.py'),'--help'],capture_output=True,text=True)
        self.assertNotEqual(0,result.returncode)
        self.assertIn('optimization is unsupported',result.stderr)

    def test_database_evidence_requires_structure_not_tokens(self):
        good={'methodName':'ExecuteScalar','operationKind':'scalar-candidate','coverageLabel':'reduced','limitations':'static only'}
        assert_database_path_evidence({'paths':[{'nodes':[{'databaseCallEvidence':good}]}]})
        for item in [None,'databaseCallEvidence',{}, {'methodName':'ExecuteScalar'}]:
            with self.subTest(item=item), self.assertRaises(ValueError):
                assert_database_path_evidence({'note':'databaseCallEvidence','paths':[{'nodes':[{'databaseCallEvidence':item}]}]})

    def setUp(self):
        self.cases={'Case000':'System.Data.Common.DbCommand.ExecuteScalar()','Case001':'System.Data.IDbCommand.ExecuteNonQuery()'}
        self.controls={'ControlFake':{'semantic':0,'projectless':0}}
        self.facts=[{'factType':'DatabaseOperationCandidate','evidenceTier':'Tier1Semantic','sourceSymbol':'Matrix.'+name+'()','properties':{'methodName':method}}
            for name,method in [('Case000','ExecuteScalar'),('Case001','ExecuteNonQuery')]]
    def test_missing_cannot_be_hidden_by_duplicate(self):
        with self.assertRaises(AssertionError): assert_calls([self.facts[0],self.facts[0]],'semantic',self.cases,self.controls)
    def test_extra_and_unmapped(self):
        extra=copy.deepcopy(self.facts[0]);extra['sourceSymbol']='Matrix.Other()'
        with self.assertRaises(AssertionError): assert_calls(self.facts+[extra],'semantic',self.cases,self.controls)
    def test_named_negative(self):
        extra=copy.deepcopy(self.facts[0]);extra['sourceSymbol']='Controls.ControlFake()'
        with self.assertRaises(AssertionError): assert_calls(self.facts+[extra],'semantic',self.cases,self.controls)
    def test_extra_candidate_in_wrong_tier_cannot_be_hidden(self):
        extra=copy.deepcopy(self.facts[0]);extra['evidenceTier']='Tier3SyntaxOrTextual'
        with self.assertRaises(AssertionError): assert_calls(self.facts+[extra],'semantic',self.cases,self.controls)
    def test_wrong_overload_member(self):
        facts=copy.deepcopy(self.facts);facts[0]['properties']['methodName']='ExecuteReader'
        with self.assertRaises(AssertionError): assert_calls(facts,'semantic',self.cases,self.controls)
    def test_all_fixture_rosters_and_control_mutations(self):
        import shutil
        for sample in (ROOT/'samples/sql-database-matrix').glob('*/csharp'):
            with self.subTest(sample=sample): load_expectations(sample,'csharp',sample.parent/'OVERLOADS.md',ROOT/'samples/sql-database-matrix/CONTROL-EXPECTATIONS.json')
        with tempfile.TemporaryDirectory() as temp:
            p=pathlib.Path(temp);sample=ROOT/'samples/sql-database-matrix/net20/csharp'
            shutil.copytree(sample,p/'sample',ignore=shutil.ignore_patterns('bin','obj'))
            matrix=p/'sample/Matrix.cs';matrix.write_text(matrix.read_text()+matrix.read_text())
            with self.assertRaises(ValueError):load_expectations(p/'sample','csharp',sample.parent/'OVERLOADS.md',ROOT/'samples/sql-database-matrix/CONTROL-EXPECTATIONS.json')
    def test_compiled_duplicates_and_missing(self):
        fact={'factType':'ManagedIlCallObserved','targetSymbol':'scope|method:7:Case000|','properties':{'targetIdentity':'x|member:13:ExecuteScalar|'}}
        with self.assertRaises(AssertionError):assert_compiled_cases([fact,fact],self.cases)
    def test_workflow_covers_producers_consumers(self):
        text=(ROOT/'.github/workflows/sql-database-compatibility.yml').read_text()
        for path in ['src/dotnet/**','samples/sql-database-matrix/**','scripts/diagnostics/SqlReferenceMatrix/**','scripts/validation/*sql*','scripts/validate-adapter-artifacts.py','scripts/test_validate_adapter_artifacts.py','rules/**','prompts/plan-backend-api-extraction.md']:
            self.assertEqual(2,text.count("'"+path+"'"))
        self.assertIn('windows-latest',text)
        self.assertIn('test_sql_matrix_expectations.py',text)
        self.assertIn('branches: [dev, main, codex/sql-database-matrix-864]',text)
        self.assertIn('FullyQualifiedName~MarkdownReportWriterTests',text)
        self.assertIn('FullyQualifiedName~SqlReviewRegressionTests',text)
        actions=re.findall(r'uses: (actions/[^@\s]+)@([^\s]+)',text)
        self.assertEqual(3,len(actions))
        for name,sha in actions:
            self.assertRegex(sha,r'^[a-f0-9]{40}$',name)

if __name__=='__main__':unittest.main()
