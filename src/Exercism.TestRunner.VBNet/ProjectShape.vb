Imports System.Text.RegularExpressions
Imports System.Xml

Namespace Global.Exercism.TestRunner.VBNet
    ''' <summary>
    ''' Reduces a project file to a key that says how it compiles: its properties, Import items and package references.
    ''' Two project files with the same key compile the same way, apart from the project name.
    ''' The key is Nothing for any project file that contains something the fast path does not know about,
    ''' such as conditions, imports of other project files, targets or other item types. Those projects always take the MSBuild path.
    ''' </summary>
    Friend Module ProjectShape
        ' These properties do not change how the project compiles or runs. RootNamespace is passed to the
        ' compiler, and the fast path fills it in (see RootNamespace).
        Private ReadOnly IgnoredProperties As New HashSet(Of String) From {"RestorePackagesWithLockFile", "IsPackable", "IsTestProject", "RootNamespace"}

        Private ReadOnly AllowedItems As New Dictionary(Of String, String()) From {
            {"PackageReference", {"Include", "Version"}},
            {"Import", {"Include"}}
        }

        ' Metadata that a PackageReference may give as child elements. It becomes part of the key.
        Private ReadOnly AllowedPackageReferenceMetadata As New HashSet(Of String) From {"IncludeAssets", "ExcludeAssets", "PrivateAssets"}

        Public Function Key(projectFileText As String) As String
            Dim root As XElement
            Try
                root = XElement.Parse(projectFileText)
            Catch exception As XmlException
                Return Nothing
            End Try

            If root.Name <> "Project" OrElse
                root.Attributes().Count() <> 1 OrElse
                CStr(root.Attribute("Sdk")) <> "Microsoft.NET.Sdk" Then
                Return Nothing
            End If

            Dim properties As New SortedDictionary(Of String, String)(StringComparer.Ordinal)
            Dim items As New List(Of String)

            For Each group In root.Elements()
                If group.HasAttributes OrElse (group.Name <> "PropertyGroup" AndAlso group.Name <> "ItemGroup") Then
                    Return Nothing
                End If

                For Each element In group.Elements()
                    If group.Name = "PropertyGroup" Then
                        If element.HasAttributes OrElse element.HasElements Then
                            Return Nothing
                        End If
                        If Not IgnoredProperties.Contains(element.Name.LocalName) Then
                            properties(element.Name.LocalName) = element.Value.Trim()
                        End If
                    Else
                        Dim allowedAttributes As String() = Nothing
                        If Not AllowedItems.TryGetValue(element.Name.LocalName, allowedAttributes) OrElse
                            element.Attributes().Any(Function(attribute) Not allowedAttributes.Contains(attribute.Name.LocalName)) Then
                            Return Nothing
                        End If
                        If element.Elements().Any(
                            Function(metadata) element.Name <> "PackageReference" OrElse
                                Not AllowedPackageReferenceMetadata.Contains(metadata.Name.LocalName) OrElse
                                metadata.HasAttributes OrElse metadata.HasElements) Then
                            Return Nothing
                        End If
                        Dim parts = element.Attributes().
                            Select(Function(attribute) $"{attribute.Name.LocalName}={attribute.Value}").
                            Concat(element.Elements().Select(Function(metadata) $"{metadata.Name.LocalName}={metadata.Value.Trim()}")).
                            OrderBy(Function(part) part, StringComparer.Ordinal)
                        items.Add(element.Name.LocalName & ":" & String.Join(";", parts))
                    End If
                Next
            Next

            If properties.Count = 0 OrElse Not IsValidRootNamespace(RootNamespaceProperty(root)) Then
                Return Nothing
            End If

            items.Sort(StringComparer.Ordinal)
            Return String.Join(vbLf, properties.Select(Function(p) $"{p.Key}={p.Value}").Concat(items))
        End Function

        ''' <summary>The project's RootNamespace property, or Nothing when it does not set one.</summary>
        Public Function RootNamespace(projectFileText As String) As String
            Return RootNamespaceProperty(XElement.Parse(projectFileText))
        End Function

        Private Function RootNamespaceProperty(root As XElement) As String
            Return root.Elements("PropertyGroup").Elements("RootNamespace").LastOrDefault()?.Value.Trim()
        End Function

        Private Function IsValidRootNamespace(rootNamespace As String) As Boolean
            Return rootNamespace Is Nothing OrElse Regex.IsMatch(rootNamespace, "^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")
        End Function
    End Module
End Namespace
