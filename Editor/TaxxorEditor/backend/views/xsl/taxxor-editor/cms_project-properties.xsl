<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="projectid"/>
	<xsl:param name="editorid"/>
	<xsl:param name="permissions"/>
	<xsl:param name="projectstatus"/>
	<xsl:param name="classicsyncenabled">no</xsl:param>


	<xsl:output encoding="UTF-8" indent="yes" method="xml" omit-xml-declaration="yes"/>

	<xsl:template match="/">
		<div>
			<xsl:attribute name="class">
				<xsl:text>project-properties</xsl:text>
				<xsl:if test="$projectstatus = 'closed'">
					<xsl:text> projectstatus_closed</xsl:text>
				</xsl:if>
			</xsl:attribute>
			<xsl:comment>
				- permissions: <xsl:value-of select="$permissions"/>
				- projectstatus: <xsl:value-of select="$projectstatus"/>
			</xsl:comment>
			<xsl:apply-templates select="/configuration/cms_projects/cms_project[@id = $projectid]"/>
		</div>
	</xsl:template>

	<xsl:template match="cms_project">
		<h5>Properties for project &quot;<span class="project_name"><xsl:value-of select="name"/></span>&quot;</h5>
		<div id="form-project-properties" class="form-horizontal">
			<div class="form-group">
				<label for="input-projectname" class="col-sm-4 control-label">Project name</label>
				<div class="col-sm-8">
					<xsl:choose>
						<xsl:when test="contains($permissions, 'editprojectproperties')">
							<input type="text" class="form-control" name="projectname" id="input-projectname" placeholder="Project Name" value="{name}">
								<xsl:if test="$projectstatus = 'closed'">
									<xsl:attribute name="disabled">
										<xsl:text>disabled</xsl:text>
									</xsl:attribute>
								</xsl:if>
							</input>
						</xsl:when>
						<xsl:otherwise>
							<span class="tx-value form-control">
								<xsl:value-of select="name"/>
							</span>
						</xsl:otherwise>
					</xsl:choose>
				</div>
			</div>
			<div class="form-group">
				<label for="select-projectstatus" class="col-sm-4 control-label">Status</label>
				<div class="col-sm-8">
					<xsl:choose>
						<xsl:when test="contains($permissions, 'editprojectproperties')">
							<select name="projectstatus" id="select-projectstatus" class="form-control">
								<xsl:call-template name="render-option-node">
									<xsl:with-param name="value">open</xsl:with-param>
									<xsl:with-param name="text">Open</xsl:with-param>
									<xsl:with-param name="default-value" select="/configuration/cms_projects/cms_project[@id = $projectid]/versions/version/status"/>
								</xsl:call-template>
								<xsl:call-template name="render-option-node">
									<xsl:with-param name="value">closed</xsl:with-param>
									<xsl:with-param name="text">Closed</xsl:with-param>
									<xsl:with-param name="default-value" select="/configuration/cms_projects/cms_project[@id = $projectid]/versions/version/status"/>
								</xsl:call-template>
							</select>
						</xsl:when>
						<xsl:otherwise>
							<span class="tx-value form-control">
								<xsl:value-of select="/configuration/cms_projects/cms_project[@id = $projectid]/versions/version/status"/>
							</span>
						</xsl:otherwise>
					</xsl:choose>
				</div>
			</div>
			<div class="form-group">
				<label for="select-reportingperiod" class="col-sm-4 control-label">Reporting period</label>
				<div class="col-sm-8">
					<xsl:choose>
						<xsl:when test="contains($permissions, 'editprojectproperties')">
							<select name="reportingperiod" id="select-reportingperiod" class="form-control">
								<xsl:if test="$projectstatus = 'closed'">
									<xsl:attribute name="disabled">
										<xsl:text>disabled</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<xsl:choose>
									<xsl:when test="/configuration/reporting_periods/period">
										<xsl:for-each select="/configuration/reporting_periods//period">
											<option value="{@id}">
												<xsl:if test="@selected">
													<xsl:attribute name="selected">selected</xsl:attribute>
												</xsl:if>
												<xsl:value-of select="."/>
											</option>
										</xsl:for-each>
									</xsl:when>
									<xsl:otherwise>
										<xsl:for-each select="/configuration/reporting_periods/*">
											<optgroup label="{@label}">
												<xsl:for-each select="./period">
													<option value="{@id}">
														<xsl:if test="@selected">
															<xsl:attribute name="selected">selected</xsl:attribute>
														</xsl:if>
														<xsl:value-of select="."/>
													</option>
												</xsl:for-each>
											</optgroup>
										</xsl:for-each>
									</xsl:otherwise>
								</xsl:choose>
							</select>
						</xsl:when>
						<xsl:otherwise>
							<span class="tx-value form-control">
								<xsl:value-of select="/configuration/reporting_periods//period[@selected]"/>
							</span>
						</xsl:otherwise>
					</xsl:choose>

				</div>
			</div>

			<div class="form-group">
				<label for="publicationdate" class="col-sm-4 control-label">Publication date</label>
				<div class="col-sm-8">
					<xsl:choose>
						<xsl:when test="contains($permissions, 'editprojectproperties')">
							<div class="input-group">
								<span class="input-group-addon btn-calendar">
									<i class="fa fa-calendar bigger-110"/>
								</span>
								<input name="publicationdate" id="publicationdate" type="text" placeholder="Planned publication date" class="form-control input-large">
									<xsl:if test="$projectstatus = 'closed'">
										<xsl:attribute name="disabled">
											<xsl:text>disabled</xsl:text>
										</xsl:attribute>
									</xsl:if>
									<xsl:if test="@date-publication and string-length(normalize-space(@date-publication)) > 1">
										<xsl:attribute name="value">
											<xsl:value-of select="substring-before(@date-publication, 'T')"/>
										</xsl:attribute>
									</xsl:if>
								</input>
							</div>
						</xsl:when>
						<xsl:otherwise>
							<span class="tx-value form-control">
								<xsl:value-of select="substring-before(@date-publication, 'T')"/>
							</span>
						</xsl:otherwise>
					</xsl:choose>
				</div>
			</div>

			<xsl:if test="contains($permissions, 'editprojectproperties')">
				<div class="projectproperties-controls">
					<button class="btn btn-info btn-xs pull-right" id="btn-save-projectproperties" data-projectid="{$projectid}">
						<span class="glyphicon glyphicon-floppy-disk">
							<xsl:comment>.</xsl:comment>
						</span>
						<xsl:text> Save</xsl:text>
					</button>
				</div>
			</xsl:if>


			<hr/>

			<xsl:if test="contains($permissions, 'managestructureddata') or contains($permissions, 'manageexternaldata')">
				<h5>Data definitions and synchronization</h5>

				<xsl:if test="contains($permissions, 'managestructureddata')">
					<div class="form-group erp-datasets">
						<label for="select-structureddatasnapshotid" class="col-sm-4 control-label">Structured data</label>
						<div class="col-sm-8">
							<div class="input-group">
								<p class="explanation">Synchronize ERP data and manual uploads for this project</p>
								<select name="structureddatasnapshotid" id="select-structureddatasnapshotid" class="form-control">
									<xsl:if test="$projectstatus = 'closed'">
										<xsl:attribute name="disabled">
											<xsl:text>disabled</xsl:text>
										</xsl:attribute>
									</xsl:if>
									<xsl:call-template name="render-option-node">
										<xsl:with-param name="value">1</xsl:with-param>
										<xsl:with-param name="default-value">1</xsl:with-param>
										<xsl:with-param name="text">Snapshot 1</xsl:with-param>
									</xsl:call-template>
									<xsl:call-template name="render-option-node">
										<xsl:with-param name="value">2</xsl:with-param>
										<xsl:with-param name="text">Snapshot 2</xsl:with-param>
									</xsl:call-template>
								</select>
								<div class="input-group-btn">
									<div class="pull-right">
										<button class="btn btn-default btn-xs btn-sync-snapshot" id="btn-sync-snapshot" onclick="syncSnapshot('{$projectid}')">
											<xsl:if test="$projectstatus = 'closed'">
												<xsl:attribute name="disabled">
													<xsl:text>disabled</xsl:text>
												</xsl:attribute>
											</xsl:if>
											<span class="glyphicon glyphicon-refresh">
												<xsl:comment>.</xsl:comment>
											</span>
											<xsl:text> Sync</xsl:text>
										</button>

										<!-- Render an import button if we have one or more structured data services defined in the stack -->
										<xsl:if test="/configuration/taxxor/components/service/services/service[@role = 'structureddatasource-xyz']">
											<button style="margin-left: 5px" class="btn btn-default btn-xs btn-import-erpdata" id="btn-import-erpdata" onclick="importErpData('{$projectid}')">
												<xsl:if test="$projectstatus = 'closed'">
													<xsl:attribute name="disabled">
														<xsl:text>disabled</xsl:text>
													</xsl:attribute>
												</xsl:if>
												<span class="glyphicon glyphicon-upload">
													<xsl:comment>.</xsl:comment>
												</span>
												<xsl:text> Import ERP data</xsl:text>
											</button>
										</xsl:if>

									</div>

								</div>

							</div>
						</div>
					</div>

				</xsl:if>



				<div class="form-group external-datasets">
					<label for="table-project-externaldata" class="col-sm-4 control-label">External datasets</label>
					<div class="col-sm-8">
						<span>
							<xsl:attribute name="class">
								<xsl:text>no-externaldatset-label</xsl:text>
								<xsl:if test="count(/configuration/cms_projects/cms_project[@id = $projectid]/repositories/external_data/sets/set) &gt; 0">
									<xsl:text> hidden</xsl:text>
								</xsl:if>
							</xsl:attribute>
							<xsl:text>No external datasets have been defined for this project</xsl:text>
						</span>
						<table id="table-project-externaldata">
							<xsl:attribute name="class">
								<xsl:text>table table-hover table-condensed table-striped</xsl:text>
								<xsl:if test="count(/configuration/cms_projects/cms_project[@id = $projectid]/repositories/external_data/sets/set) = 0">
									<xsl:text> hidden</xsl:text>
								</xsl:if>
							</xsl:attribute>
							<thead>
								<tr>
									<xsl:choose>
										<xsl:when test="$classicsyncenabled = 'yes'">
											<th width="40%">Identifier</th>
											<th width="40%">Dataset</th>
											<th width="20%"/>
										</xsl:when>
										<xsl:otherwise>
											<th width="40%">Identifier</th>
											<th width="65%">Dataset</th>
											<th width="5%"/>
										</xsl:otherwise>
									</xsl:choose>
								</tr>
							</thead>
							<tbody>
								<xsl:apply-templates select="/configuration/cms_projects/cms_project[@id = $projectid]/repositories/external_data/sets/set"/>
							</tbody>
						</table>

						<div class="buttons pull-right externaldataset-buttons">
							<xsl:if test="count(/configuration/cms_projects/cms_project[@id = $projectid]/repositories/external_data/sets/set) = 0">
								<xsl:attribute name="style">padding-top: 7px;</xsl:attribute>
							</xsl:if>
							<xsl:choose>
								<xsl:when test="$projectstatus = 'closed'">
									<xsl:comment>.</xsl:comment>
								</xsl:when>
								<xsl:otherwise>
									<button class="btn btn-primary btn-xs" id="btn-add-externaldataset" onclick="addExternalDataSetToDomTable('{$projectid}')">
										<span class="glyphicon glyphicon-plus">
											<xsl:comment>.</xsl:comment>
										</span>
										<xsl:text> Add external data set</xsl:text>
									</button>

									<button id="btn-save-externaldatasets" data-projectid="{$projectid}">
										<xsl:attribute name="class">
											<xsl:text>btn btn-info btn-xs</xsl:text>
											<xsl:if test="count(/configuration/cms_projects/cms_project[@id = $projectid]/repositories/external_data/sets/set) = 0">
												<xsl:text> hidden</xsl:text>
											</xsl:if>
										</xsl:attribute>
										<span class="glyphicon glyphicon-floppy-disk">
											<xsl:comment>.</xsl:comment>
										</span>
										<xsl:text> Save</xsl:text>
									</button>
								</xsl:otherwise>
							</xsl:choose>
						</div>
					</div>
				</div>


				<hr/>

			</xsl:if>


			<h5>Regulatory information</h5>
			<div class="form-group">
				<label for="table-project-reportingrequirements" class="col-sm-4 control-label">Reporting requirements</label>
				<div class="col-sm-8">
					<table id="table-project-reportingrequirements" class="table table-hover table-condensed table-striped">
						<thead>
							<tr>
								<th width="40%">Name</th>
								<th width="40%">Output channel</th>
								<th width="20%"/>
							</tr>
						</thead>
						<tbody>
							<xsl:choose>
								<xsl:when test="/configuration/cms_projects/cms_project[@id = $projectid]/reporting_requirements/reporting_requirement">
									<xsl:apply-templates select="/configuration/cms_projects/cms_project[@id = $projectid]/reporting_requirements/reporting_requirement"/>
								</xsl:when>
								<xsl:otherwise>
									<tr class="dummy">
										<td colspan="3">No reporting requirement defined</td>
									</tr>
								</xsl:otherwise>
							</xsl:choose>

						</tbody>
					</table>
					<!--
					<div class="buttons pull-right">
						<button class="btn btn-primary btn-xs pull-right" id="btn-add-usergroup" onclick="addReportingRequirementToDomTable('{$projectid}')">
							<span class="glyphicon glyphicon-plus">
								<xsl:comment>.</xsl:comment>
							</span> Add reporting requirement</button>
					</div>
					-->
				</div>
			</div>

		</div>

		<div class="html-templates">
			<table id="table-project-externaldata-templates" class="table table-condensed">
				<tbody>
					<xsl:call-template name="render-externaldata-row">
						<xsl:with-param name="mode">template</xsl:with-param>
					</xsl:call-template>
				</tbody>
			</table>
		</div>


		<xsl:if test="contains($permissions, 'manageacl')">
			<hr/>

			<h5>Access rights <small>note that changes will immediately be saved on the server</small></h5>
			<div id="access-control-wrapper">
				<xsl:comment>.</xsl:comment>
			</div>

			<hr class="min"/>

			<div id="access-control-wrapper-bulkoperations">
				<div class="form-group">
					<div class="pull-left">
						<label for="btn-explicitaccessrecords-enable" class="tx-labelexplicitacl">Report sections: enable/disable access control settings <small>(current state: <span>...</span>)</small></label>
					</div>
					<div class="pull-right">
						<button class="btn btn-info btn-xs" id="btn-explicitaccessrecords-enable" onclick="enableDisableExplicitOutputChannelAccessRecords('{$projectid}', true)">
							<xsl:text>Enable</xsl:text>
						</button>
						<button class="btn btn-info btn-xs" id="btn-explicitaccessrecords-disable" onclick="enableDisableExplicitOutputChannelAccessRecords('{$projectid}', false)">
							<xsl:text>Disable</xsl:text>
						</button>
					</div>
				</div>
			</div>
		</xsl:if>


	</xsl:template>

	<!-- Renders an entry in the external data table -->
	<xsl:template match="set">
		<xsl:call-template name="render-externaldata-row">
			<xsl:with-param name="position" select="position()"/>
			<xsl:with-param name="id" select="@id"/>
			<xsl:with-param name="name" select="name"/>
		</xsl:call-template>
	</xsl:template>


	<!-- Renders an external data table row -->
	<xsl:template name="render-externaldata-row">
		<xsl:param name="mode">normal</xsl:param>
		<xsl:param name="position"/>
		<xsl:param name="name"/>
		<xsl:param name="id"/>

		<xsl:variable name="row-id">
			<xsl:text>externaldata-row-</xsl:text>
			<xsl:choose>
				<xsl:when test="$mode = 'template'">[position]</xsl:when>
				<xsl:otherwise>
					<xsl:value-of select="$position"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<tr id="{$row-id}">
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="$mode = 'template'">new</xsl:when>
					<xsl:otherwise>existing</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<td class="external-dataset-name">
				<span class="fixed">
					<xsl:choose>
						<xsl:when test="$mode = 'template'"/>
						<xsl:otherwise>
							<xsl:value-of select="$name"/>
						</xsl:otherwise>
					</xsl:choose>
					<xsl:text> </xsl:text>
				</span>
				<span class="input">
					<input type="text">
						<xsl:if test="$projectstatus = 'closed'">
							<xsl:attribute name="disabled">
								<xsl:text>disabled</xsl:text>
							</xsl:attribute>
						</xsl:if>
						<xsl:attribute name="name">
							<xsl:choose>
								<xsl:when test="$mode = 'normal'">
									<xsl:value-of select="concat('externaldatasetname-', $position)"/>
								</xsl:when>
								<xsl:otherwise>
									<xsl:text>externaldatasetname-[position]</xsl:text>
								</xsl:otherwise>
							</xsl:choose>
						</xsl:attribute>
						<xsl:attribute name="id">
							<xsl:choose>
								<xsl:when test="$mode = 'normal'">
									<xsl:value-of select="concat('externaldatasetname-', $position)"/>
								</xsl:when>
								<xsl:otherwise>
									<xsl:text>externaldatasetname-[position]</xsl:text>
								</xsl:otherwise>
							</xsl:choose>
						</xsl:attribute>
						<xsl:attribute name="value">
							<xsl:choose>
								<xsl:when test="$mode = 'template'"/>
								<xsl:otherwise>
									<xsl:value-of select="$name"/>
								</xsl:otherwise>
							</xsl:choose>
						</xsl:attribute>
					</input>
				</span>
			</td>
			<td class="external-dataset-set">
				<select name="select-externaldatasetid-{$position}" id="select-externaldatasetid-{$position}" class="form-control">
					<xsl:if test="$projectstatus = 'closed'">
						<xsl:attribute name="disabled">
							<xsl:text>disabled</xsl:text>
						</xsl:attribute>
					</xsl:if>
					<option value="none">-- select set --</option>
					<xsl:for-each select="/configuration/generic_data/workbooks/workbook">
						<xsl:call-template name="render-option-node">
							<xsl:with-param name="value" select="@id"/>
							<xsl:with-param name="text" select="@id"/>
							<xsl:with-param name="default-value" select="$id"/>
						</xsl:call-template>
					</xsl:for-each>
				</select>
			</td>
			<td>
				<span class="pull-right">
					<xsl:choose>
						<xsl:when test="$mode = 'template'">
							<button title="Delete external dataset" class="btn btn-danger btn-xs" onclick="removeExternalDataSetFromDomTable('{$projectid}', '{$row-id}')">
								<xsl:if test="$projectstatus = 'closed'">
									<xsl:attribute name="disabled">
										<xsl:text>disabled</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<span class="glyphicon glyphicon-trash">
									<xsl:comment>.</xsl:comment>
								</span>
							</button>
						</xsl:when>
						<xsl:when test="$classicsyncenabled = 'yes'">
							<button title="Synchronize external dataset" class="btn btn-default btn-xs pull-right btn-sync-externaldata" id="btn-sync-externaldata" onclick="syncExternalTables('{$projectid}', '{$name}', '{$id}')">
								<xsl:if test="$projectstatus = 'closed'">
									<xsl:attribute name="disabled">
										<xsl:text>disabled</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<span class="glyphicon glyphicon-refresh">
									<xsl:comment>.</xsl:comment>
								</span>
								<xsl:text> Sync</xsl:text>
							</button>
						</xsl:when>
					</xsl:choose>
				</span>
			</td>
		</tr>

	</xsl:template>

	<xsl:template match="reporting_requirement">
		<xsl:variable name="outputchannelvariant-ref" select="@ref-outputchannelvariant"/>
		<xsl:variable name="outputchannelname" select="/configuration/editors/editor[@id = $editorid]/output_channels/output_channel/variants/variant[@id = $outputchannelvariant-ref]/name"/>
		<tr>
			<td>
				<xsl:value-of select="name"/>
			</td>
			<td>
				<xsl:value-of select="$outputchannelname"/>
			</td>
			<td> </td>
		</tr>
	</xsl:template>


	<xsl:template name="render-option-node">
		<xsl:param name="value"/>
		<xsl:param name="text"/>
		<xsl:param name="default-value"/>
		<xsl:param name="class"/>

		<option value="{$value}">

			<xsl:if test="string-length(normalize-space($default-value)) &gt; 0 and string($value) = string($default-value)">
				<xsl:attribute name="selected">selected</xsl:attribute>
			</xsl:if>
			<xsl:value-of select="$text"/>
		</option>
	</xsl:template>


</xsl:stylesheet>
