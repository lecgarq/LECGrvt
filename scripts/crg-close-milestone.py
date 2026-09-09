"""Explicit milestone maintenance, never a lecg-map-codebase query route.

Run with the Python environment that already contains code-review-graph.
Uses CRG's own snapshot/diff API; does not install hooks or compute embeddings.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from code_review_graph.graph import GraphStore
from code_review_graph.graph_diff import take_snapshot, load_snapshot, save_snapshot, diff_snapshots


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='HEAD~1', help='Explicit Git base for impact analysis')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    directory = repo / '.code-review-graph'
    database = directory / 'graph.db'
    checkpoint = directory / 'milestone.json'
    command = shutil.which('code-review-graph')
    if not command or not database.is_file():
        parser.error('Build the graph with the installed code-review-graph CLI first.')
    with GraphStore(database) as store:
        first_checkpoint = not checkpoint.exists()
        before = take_snapshot(store) if first_checkpoint else load_snapshot(checkpoint)
    # Each step must succeed before publishing a new checkpoint. No watchers or model calls.
    subprocess.run([command, 'update', '--repo', str(repo)], cwd=repo, check=True)
    subprocess.run([command, 'detect-changes', '--brief', '--base', args.base,
                    '--repo', str(repo)], cwd=repo, check=True)
    with GraphStore(database) as store:
        after = take_snapshot(store)
    delta = diff_snapshots(before, after)
    print(json.dumps({'first_checkpoint': first_checkpoint,
                      'baseline': 'pre-update graph' if first_checkpoint else 'previous milestone',
                      'graph_diff': delta}, ensure_ascii=False))
    temporary = checkpoint.with_suffix('.json.tmp')
    save_snapshot(after, temporary)
    temporary.replace(checkpoint)


if __name__ == '__main__':
    main()
